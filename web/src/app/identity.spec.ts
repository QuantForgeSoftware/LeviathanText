import { authorId, DefaultAuthor, setAuthorId } from './identity';

describe('workspace identity', () => {
  afterEach(() => localStorage.clear());

  it('uses the stored name and falls back to the default', () => {
    expect(authorId()).toBe(DefaultAuthor);
    setAuthorId('reviewer-a');
    expect(authorId()).toBe('reviewer-a');
    setAuthorId('   ');
    expect(authorId()).toBe(DefaultAuthor);
  });
});
