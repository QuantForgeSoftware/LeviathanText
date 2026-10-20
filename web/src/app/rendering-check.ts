/**
 * Transcription rendering check (project-plan §4: font/combining-mark rendering checks and the
 * polytonic input palette). The fixture strings are the labeled INVENTED examples from
 * docs/contracts/examples.md — displayed to verify that combining sequences, lunate
 * sigma and overbars render distinctly in the browser. They are not corpus data.
 */
import { Component, signal } from '@angular/core';

@Component({
  selector: 'app-rendering-check',
  standalone: true,
  template: `
    <section>
      <h2>Polytonic rendering check</h2>
      <table>
        <tr><th>Case</th><th>Text</th><th>Scalars (expected)</th></tr>
        @for (row of rows(); track row.label) {
          <tr>
            <td>{{ row.label }}</td>
            <td class="greek">{{ row.text }}</td>
            <td>{{ scalars(row.text) }} ({{ row.expected }})</td>
          </tr>
        }
      </table>
      <p>Each scalar count is computed in the browser and must match the fixture
         expectation — a mismatch means the combining sequence was broken on the wire.</p>
    </section>
  `,
  styles: [`.greek { font-size: 1.4rem; }`],
})
export class RenderingCheck {
  protected readonly rows = signal([
    { label: 'EX-2 precomposed ἄνθρωπος', text: 'ἄνθρωπος', expected: 9 },
    { label: 'EX-2 decomposed ἄνθρωπος', text: '\u03B1\u0313\u0301\u03BD\u03B8\u03C1\u03C9\u03C0\u03BF\u03C2', expected: 10 },
    { label: 'EX-3 scriptio continua with lunate sigma', text: 'ΕΑΝΤΙϹΑΛΛΟΓΡΑΦΗ', expected: 15 },
    { label: 'EX-1 overbar nomen sacrum', text: '\u039A\u0305\u03F2\u0305', expected: 4 },
    { label: 'EX-5 perispomeni on ε (annotation-only mark)', text: 'εα\u0342ντι', expected: 5 },
  ]);

  protected scalars(text: string): number {
    return scalarCount(text);
  }
}

function scalarCount(text: string): number {
  let count = 0;
  for (const _ of text) count++;
  return count;
}
