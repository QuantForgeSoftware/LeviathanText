import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { GeometryEditor } from './geometry-editor';

describe('GeometryEditor', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GeometryEditor],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('serializes polygon and baseline coordinates in image-frame units', () => {
    const fixture = TestBed.createComponent(GeometryEditor);
    const component = fixture.componentInstance as unknown as {
      polygonPoints(line: { polygon: { x: number; y: number }[] }): string;
      baselinePoints(line: { baseline: { x: number; y: number }[] }): string;
    };
    expect(component.polygonPoints({ polygon: [{ x: 10.5, y: 20 }, { x: 30, y: 40.25 }] }))
      .toBe('10.5,20 30,40.25');
    expect(component.baselinePoints({ baseline: [{ x: 1, y: 2 }, { x: 3, y: 4 }] }))
      .toBe('1,2 3,4');
  });

  it('renders without project data (offline-safe)', () => {
    const fixture = TestBed.createComponent(GeometryEditor);
    fixture.componentRef.setInput('projectId', 'col-offline');
    fixture.componentRef.setInput('surfaceId', 'surf-offline');
    fixture.componentRef.setInput('imageUrl', '');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('h3')?.textContent).toContain('Geometry');
  });
});
