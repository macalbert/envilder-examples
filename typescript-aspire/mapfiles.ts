import { readFileSync } from 'node:fs';
import { MapFileParser, type ParsedMapFile } from '@envilder/sdk';

/**
 * Reads a shared map file and gives every mapped secret a name no other test
 * uses, keeping `$config` as committed.
 */
export function withUniqueSecrets(
  path: string,
  unique: (secret: string) => string,
): ParsedMapFile {
  const mapFile = JSON.parse(readFileSync(path, 'utf8')) as Record<
    string,
    unknown
  >;
  for (const [variable, secret] of Object.entries(mapFile)) {
    if (!variable.startsWith('$')) {
      mapFile[variable] = unique(secret as string);
    }
  }
  return new MapFileParser().parse(JSON.stringify(mapFile));
}
