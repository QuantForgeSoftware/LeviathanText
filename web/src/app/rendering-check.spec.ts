import { TestBed } from '@angular/core/testing';
import { RenderingCheck } from './rendering-check';

/** These invented display examples are not witness transcriptions or quality labels. */
describe('RenderingCheck fixture scalar expectations', () => {
  const examples = [
    { label: 'precomposed', text: 'ἄνθρωπος', scalars: 8 },
    { label: 'decomposed', text: '\u03b1\u0313\u0301\u03bd\u03b8\u03c1\u03c9\u03c0\u03bf\u03c2', scalars: 10 },
    { label: 'lunate sigma', text: 'ΕΑΝΤΙϹΑΛΛΟΓΡΑΦΗ', scalars: 15 },
    { label: 'overbar', text: '\u039a\u0305\u03f2\u0305', scalars: 4 },
    { label: 'annotation-only combining mark', text: 'εα\u0342ντι', scalars: 6 },
  ];
  for (const example of examples) {
    it(`shows the actual and expected ${example.scalars} scalars for ${example.label}`, () => {
      TestBed.configureTestingModule({ imports: [RenderingCheck] });
      const fixture = TestBed.createComponent(RenderingCheck); fixture.detectChanges();
      const rows = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('table tr')).slice(1);
      const row = rows.find(value => value.querySelector('td.greek')?.textContent === example.text);
      expect(row).withContext('The invented example must remain byte-equivalent; do not normalize its combining sequence.').toBeDefined();
      expect(Array.from(example.text).length).toBe(example.scalars);
      expect(row?.querySelector('td:last-child')?.textContent?.trim()).toBe(`${example.scalars} (${example.scalars})`);
    });
  }
});
