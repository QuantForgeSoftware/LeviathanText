/** Generated display metadata for component tests; never a witness/image acceptance fixture. */
import { ComponentFixture } from '@angular/core/testing';
import { CaptureDisplayDescriptor } from './api-client';
import { GeometryEditor } from './geometry-editor';

export function displayFixture(projectId = 'col1', surfaceId = 'surf1', overrides: Partial<CaptureDisplayDescriptor> = {}): CaptureDisplayDescriptor {
  return {
    schemaVersion: 1, projectId, surfaceId, captureId: 'cap1', sourceFrameRef: 'cap1',
    recordedSourceChecksum: { algorithm: 'unavailable', digest: 'generated' }, recordedChecksumStatus: 'unavailable',
    observedSourceChecksum: { algorithm: 'sha256', digest: 'a'.repeat(64) },
    sourceWidth: 1000, sourceHeight: 1000, displayWidth: 1000, displayHeight: 1000,
    scaleX: 1, scaleY: 1, orientation: 'raw', mediaType: 'image/png', derivativeId: 'b'.repeat(64),
    displayChecksum: { algorithm: 'sha256', digest: 'c'.repeat(64) }, ...overrides,
  };
}

export function prepareGeometryDisplay(fixture: ComponentFixture<GeometryEditor>, projectId = 'col1', surfaceId = 'surf1'): void {
  fixture.componentRef.setInput('display', displayFixture(projectId, surfaceId));
  fixture.componentRef.setInput('imageUrl', 'blob:generated-geometry-fixture');
}

export function markGeometryImageLoaded(fixture: ComponentFixture<GeometryEditor>): void {
  const image = fixture.nativeElement.querySelector('img') as HTMLImageElement;
  Object.defineProperty(image, 'naturalWidth', { configurable: true, value: 1000 });
  Object.defineProperty(image, 'naturalHeight', { configurable: true, value: 1000 });
  image.dispatchEvent(new Event('load')); fixture.detectChanges();
}
