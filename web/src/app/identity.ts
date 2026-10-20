/** Workspace identity used as the revision author and review reviewer (local pilot). */
const StorageKey = 'leviathan:author';
export const DefaultAuthor = 'workspace-editor';

export function authorId(): string {
  try {
    return globalThis.localStorage?.getItem(StorageKey)?.trim() || DefaultAuthor;
  } catch {
    return DefaultAuthor;
  }
}

export function setAuthorId(value: string): void {
  try {
    const trimmed = value.trim();
    if (trimmed) globalThis.localStorage?.setItem(StorageKey, trimmed);
    else globalThis.localStorage?.removeItem(StorageKey);
  } catch {
    /* ignore */
  }
}
