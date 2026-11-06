"use strict";

// Static interpreter: configuration is data, never evaluated as JavaScript source.
function evalAst(ast, value, limit) {
  if (ast[0] === "value") return value;
  if (ast[0] === "limit") return limit;
  if (ast[0] === "int") return ast[1];
  if (ast[0] === "and")
    return evalAst(ast[1], value, limit) && evalAst(ast[2], value, limit);
  if (ast[0] === "gt")
    return evalAst(ast[1], value, limit) > evalAst(ast[2], value, limit);
  throw new Error("Unsupported expression: " + ast[0]);
}

function trim(text) {
  return text.replace(/^[ \t\r\n]+|[ \t\r\n]+$/g, "");
}

function textAst(ast, raw) {
  if (ast[0] === "raw") return raw;
  if (ast[0] === "string") return ast[1];
  if (ast[0] === "replace") {
    const source = textAst(ast[1], raw);
    const find = textAst(ast[2], raw);
    if (!find) throw new Error("Empty replace pattern is unsupported");
    return source.split(find).join(textAst(ast[3], raw));
  }
  throw new Error("Unsupported text view: " + ast[0]);
}

function viewOf(raw, view) {
  if (!view) return raw;
  if (view.kind === "builtIn" && view.builtIn === "Trim") return trim(raw);
  if (view.kind === "custom") return textAst(view.expression, raw);
  throw new Error("Unsupported view transform");
}

function toInt(text) {
  const cleaned = trim(text);
  if (!/^[+-]?[0-9]+$/.test(cleaned)) return null;
  const result = Number(cleaned);
  return Number.isInteger(result) &&
    result >= -2147483648 &&
    result <= 2147483647
    ? result
    : null;
}

function validate(raw, limit, manifest, blocking = false) {
  const original = raw == null ? "" : raw;
  for (const validator of manifest.validators) {
    if (blocking && !validator.blockInput) continue;
    const view = viewOf(original, validator.viewTransform);
    if (
      (validator.kind === "Required" && !view.length) ||
      (validator.kind === "MaxLength" && view.length > validator.maximum)
    ) {
      return validator.message;
    }
    if (blocking) continue; // Only candidate-input-safe validators can block.
    if (validator.kind === "IsInt" || validator.convertTo === "Int32") {
      if (!trim(view).length && validator.kind !== "IsInt") continue;
      const number = toInt(view);
      if (number === null) return validator.message || "Enter a valid value.";
      if (
        (validator.kind === "Range" && number > validator.maximum) ||
        (validator.kind === "NonZero" && number === 0) ||
        (validator.kind === "Custom" &&
          !evalAst(validator.predicate, number, limit))
      ) {
        return validator.message;
      }
    }
  }
  return null;
}

function evaluate(raw, limit, manifest) {
  const original = raw == null ? "" : raw;
  // Conversion and field transformations belong to the server. A validator's
  // temporary view is never committed to the DOM or field value here.
  return { raw: original, error: validate(raw, limit, manifest) };
}

function shouldBlock(candidate, limit, manifest) {
  return validate(candidate, limit, manifest, true) !== null;
}

const api = { evaluate, shouldBlock };
if (typeof module !== "undefined" && module.exports) module.exports = api;
else globalThis.fieldExpressionPrototype = api;
