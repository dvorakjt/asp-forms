"use strict";

const assert = require("node:assert/strict");
const { execFileSync } = require("node:child_process");
const { join } = require("node:path");
const { evaluate, shouldBlock } = require("../Expression/field-client");

const root = join(__dirname, "..", "..");
const lines = execFileSync(
  "mono",
  [join(__dirname, "bin/Debug/net48/Checks.exe")],
  { encoding: "utf8" },
)
  .trimEnd()
  .split(/\r?\n/);
const cases = lines
  .filter((line) => line.startsWith("CASE\t"))
  .map((line) => JSON.parse(line.slice(5)));
for (const [index, test] of cases.entries()) {
  assert.equal(
    evaluate(test.raw, test.limit, test.manifest).error,
    test.result.error,
    `C#/JS validation parity case ${index}: ${test.raw}`,
  );
  assert.equal(evaluate(test.raw, test.limit, test.manifest).raw, test.raw);
}

const defaultRules = cases[0].manifest;
assert.equal(
  defaultRules.validators.filter((v) => v.kind === "IsInt").length,
  1,
);
const explicit = cases.find(
  (c) =>
    c.raw === "nope" &&
    c.manifest.validators.some(
      (v) => v.message === "Use digits for the revision.",
    ),
);
assert.equal(
  explicit.manifest.validators.filter((v) => v.kind === "IsInt").length,
  1,
);
assert.equal(explicit.result.error, "Use digits for the revision.");
assert.equal(
  cases.find((c) => c.raw === " " && c.result.error).result.error,
  "Enter a revision.",
  "Required trims its view before conversion",
);
assert.equal(
  cases.find(
    (c) =>
      c.raw === "nope" &&
      c.manifest.validators.some(
        (v) => v.message === "Enter a whole-number revision.",
      ),
  ).result.error,
  "Enter a whole-number revision.",
);
assert.equal(
  cases.find((c) => c.raw === "1234567890123").result.error,
  "Revision is too long.",
  "The earlier validator wins",
);
assert.equal(
  cases.find((c) => c.raw === "1" && c.limit === 0).result.error,
  "Revision and limit must be positive.",
);
assert.equal(cases.find((c) => c.raw === "  Draft  ").result.text, "Draft");
assert.equal(
  cases.find((c) => c.raw === "  Draft  ").manifest.transformers[0].message,
  null,
);
assert.equal(
  cases.find(
    (c) =>
      c.raw === "0" &&
      c.manifest.transformers.some((t) => t.kind === "RejectZero") &&
      c.manifest.validators.some(
        (v) => v.message === "Zero is not a valid revision.",
      ),
  ).result.error,
  "Zero is not a valid revision.",
);
assert.equal(
  cases.find(
    (c) =>
      c.raw === "nope" &&
      c.manifest.transformers.some((t) => t.kind === "RejectZero"),
  ).result.error,
  "Enter a valid value.",
);
assert.equal(
  cases.find(
    (c) =>
      c.raw === "0" &&
      c.manifest.validators.some(
        (v) => v.message === "Use a nonzero revision.",
      ),
  ).result.error,
  "Use a nonzero revision.",
);
assert.equal(shouldBlock("1234567890123", 5, defaultRules), true);
assert.equal(
  shouldBlock("-", 5, defaultRules),
  false,
  "Allow incomplete numeric edits",
);
assert.equal(shouldBlock("123", 5, defaultRules), false);

// Transpose is an independent, earlier feasibility spike, not the new field API.
const transpiled = execFileSync(
  "node",
  [join(root, "Prototypes/Transpose/bin/transpose-spike.js")],
  { encoding: "utf8" },
)
  .trimEnd()
  .split(/\r?\n/);
assert.equal(transpiled.length, 9);
console.log(
  `Passed ${cases.length} C#/JS field-pipeline parity cases, ModelState, ` +
    "implicit/explicit validator, and blocking checks; Transpose spike still runs.",
);
