'use strict';

const assert = require('node:assert/strict');
require('../../Content/typed-rules.js');
const rules = globalThis.FormsSandbox.Forms2.Shared.ValueRules;
const cases = [
  { input: '\u0085 abc \u0085', expected: 'abc' },
  { input: '\u00a0 abc \u00a0', expected: 'abc' },
  { input: ' abc ', expected: 'abc' },
];
for (const test of cases) assert.equal(rules.Trim(test.input), test.expected);
assert.equal(rules.AsciiDigits('\u0661'), false);
assert.equal(rules.AsciiDigits('123'), true);
assert.equal(rules.DecimalSyntax('0.12345678901234567890123456789'), false);
assert.equal(rules.DecimalSyntax('0.1234567890123456789012345678'), true);
assert.equal(rules.FloatingSyntax('0.12345678901234567890123456789'), true);
console.log('Transpose shared-rule Unicode, digit and decimal fixtures passed.');
