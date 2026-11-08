import { createCipheriv, createDecipheriv, randomBytes } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';

// A demo-only "secret store": a JSON file whose values are encrypted with
// AES-256-GCM. Each value is base64(iv | ciphertext | auth tag). The same
// format is used by the Python and .NET examples, so a vault sealed by one
// opens in the others. Do NOT use this as a real secret store.

const IV_LENGTH = 12;
const TAG_LENGTH = 16;

export type Vault = Record<string, string>;

export function generateKey(): Buffer {
  return randomBytes(32);
}

export function seal(key: Buffer, plaintext: string): string {
  const iv = randomBytes(IV_LENGTH);
  const cipher = createCipheriv('aes-256-gcm', key, iv);
  const ciphertext = Buffer.concat([
    cipher.update(plaintext, 'utf8'),
    cipher.final(),
  ]);
  return Buffer.concat([iv, ciphertext, cipher.getAuthTag()]).toString(
    'base64',
  );
}

export function open(key: Buffer, sealed: string): string {
  const bytes = Buffer.from(sealed, 'base64');
  const iv = bytes.subarray(0, IV_LENGTH);
  const tag = bytes.subarray(bytes.length - TAG_LENGTH);
  const ciphertext = bytes.subarray(IV_LENGTH, bytes.length - TAG_LENGTH);
  const decipher = createDecipheriv('aes-256-gcm', key, iv);
  decipher.setAuthTag(tag);
  return Buffer.concat([
    decipher.update(ciphertext),
    decipher.final(),
  ]).toString('utf8');
}

export function writeVault(path: string, vault: Vault): void {
  writeFileSync(path, JSON.stringify(vault, null, 2));
}

export function readVault(path: string): Vault {
  return JSON.parse(readFileSync(path, 'utf8')) as Vault;
}
