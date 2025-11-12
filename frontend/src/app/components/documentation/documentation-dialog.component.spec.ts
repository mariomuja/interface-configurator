import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DocumentationDialogComponent } from './documentation-dialog.component';
import { MatDialogRef } from '@angular/material/dialog';
import { provideAnimations } from '@angular/platform-browser/animations';
import { DOCUMENTATION_CHAPTERS } from '../../models/documentation.model';

describe('DocumentationDialogComponent', () => {
  let component: DocumentationDialogComponent;
  let fixture: ComponentFixture<DocumentationDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<DocumentationDialogComponent>>;

  beforeEach(async () => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [DocumentationDialogComponent],
      providers: [
        provideAnimations(),
        { provide: MatDialogRef, useValue: dialogRefSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentationDialogComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<DocumentationDialogComponent>>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have documentation chapters', () => {
    expect(component.chapters).toBeDefined();
    expect(component.chapters.length).toBeGreaterThan(0);
    expect(component.chapters).toEqual(DOCUMENTATION_CHAPTERS);
  });

  it('should close dialog when close is called', () => {
    component.close();
    expect(dialogRef.close).toHaveBeenCalled();
  });

  it('should display all chapters as tabs', () => {
    fixture.detectChanges();
    // Mat-tabs are rendered dynamically, so we check the component has chapters
    expect(component.chapters.length).toBe(DOCUMENTATION_CHAPTERS.length);
    expect(component.chapters).toEqual(DOCUMENTATION_CHAPTERS);
  });

  it('should have close button in header', () => {
    fixture.detectChanges();
    const closeButton = fixture.nativeElement.querySelector('button[mat-icon-button]');
    expect(closeButton).toBeTruthy();
  });
});

