import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

// PostCSS discovery parses package.json directly; a UTF-8 BOM breaks CSS loading.
for (const name of ['package.json', 'package-lock.json', 'tsconfig.json']) {
  test(`${name} is valid JSON without a byte order mark`, async () => {
    const source = await readFile(new URL(`../../src/web/${name}`, import.meta.url), 'utf8');
    assert.notEqual(source.charCodeAt(0), 0xfeff, 'Save JSON as UTF-8 without BOM');
    assert.doesNotThrow(() => JSON.parse(source));
  });
}