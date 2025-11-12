import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DocumentationComponent } from './documentation.component';
import { MatDialog } from '@angular/material/dialog';
import { provideAnimations } from '@angular/platform-browser/animations';
import { DocumentationDialogComponent } from './documentation-dialog.component';
import { DOCUMENTATION_CHAPTERS } from '../../models/documentation.model';

describe('DocumentationComponent', () => {
  let component: DocumentationComponent;
  let fixture: ComponentFixture<DocumentationComponent>;
  let dialog: jasmine.SpyObj<MatDialog>;

  beforeEach(async () => {
    const dialogSpy = jasmine.createSpyObj('MatDialog', ['open']);

    await TestBed.configureTestingModule({
      imports: [DocumentationComponent],
      providers: [
        provideAnimations(),
        { provide: MatDialog, useValue: dialogSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentationComponent);
    component = fixture.componentInstance;
    dialog = TestBed.inject(MatDialog) as jasmine.SpyObj<MatDialog>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have documentation chapters', () => {
    expect(component.chapters).toBeDefined();
    expect(component.chapters.length).toBeGreaterThan(0);
    expect(component.chapters).toEqual(DOCUMENTATION_CHAPTERS);
  });

  it('should open documentation dialog when button is clicked', () => {
    const dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);
    dialog.open.and.returnValue(dialogRef);
    
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button');
    expect(button).toBeTruthy();
    
    component.openDocumentation();
    
    expect(dialog.open).toHaveBeenCalledWith(DocumentationDialogComponent, {
      width: '90%',
      maxWidth: '800px',
      maxHeight: '90vh',
      panelClass: 'documentation-dialog'
    });
  });

  it('should have help icon button', () => {
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button');
    expect(button).toBeTruthy();
    expect(button.getAttribute('aria-label')).toBe('Dokumentation');
  });
});

