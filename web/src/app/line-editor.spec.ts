import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from './api-client';
import { LineEditor } from './line-editor';

describe('LineEditor (crash recovery)', () => {
  const lineId = 'ln-draft-test';

  function setup(serverText: string) {
    const stub = {
      line: () => of({ line: lineId, diplomatic: serverText, history: [] }),
      appendRevision: () => of({ revisionId: 'rev-new' }),
      addAnnotation: () => of({ annotationId: 'ann-1' }),
    };
    TestBed.configureTestingModule({
      imports: [LineEditor],
      providers: [{ provide: ApiClient, useValue: stub }],
    });
    const fixture = TestBed.createComponent(LineEditor);
    fixture.componentRef.setInput('lineId', lineId);
    fixture.componentRef.setInput('authorId', 'tester');
    return fixture;
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('restores an unsaved draft that differs from the server text', () => {
    localStorage.setItem(`leviathan:draft:${lineId}`, 'draft text');
    const fixture = setup('server text');
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.value).toBe('draft text');
    expect(fixture.nativeElement.textContent).toContain('Unsaved draft restored');
  });

  it('ignores a stale draft identical to the server text', () => {
    localStorage.setItem(`leviathan:draft:${lineId}`, 'same text');
    const fixture = setup('same text');
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.value).toBe('same text');
    expect(fixture.nativeElement.textContent).not.toContain('Unsaved draft restored');
  });

  it('uses the server text when no draft exists', () => {
    const fixture = setup('server only');
    fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.value).toBe('server only');
  });
});
