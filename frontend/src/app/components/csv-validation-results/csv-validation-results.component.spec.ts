import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CsvValidationResultsComponent } from './csv-validation-results.component';
import { TransportService } from '../../services/transport.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('CsvValidationResultsComponent', () => {
  let component: CsvValidationResultsComponent;
  let fixture: ComponentFixture<CsvValidationResultsComponent>;
  let transportService: jasmine.SpyObj<TransportService>;

  beforeEach(async () => {
    const transportServiceSpy = jasmine.createSpyObj('TransportService', ['validateCsvFile']);

    await TestBed.configureTestingModule({
      imports: [
        CsvValidationResultsComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: TransportService, useValue: transportServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CsvValidationResultsComponent);
    component = fixture.componentInstance;
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit', () => {
    it('should validate if blobPath is provided', () => {
      component.blobPath = 'test/path.csv';
      transportService.validateCsvFile.and.returnValue(of({ valid: true }));
      
      component.ngOnInit();
      
      expect(transportService.validateCsvFile).toHaveBeenCalled();
    });

    it('should not validate if blobPath is empty', () => {
      component.blobPath = '';
      component.ngOnInit();
      
      expect(transportService.validateCsvFile).not.toHaveBeenCalled();
    });
  });

  describe('validate', () => {
    it('should validate CSV file successfully', () => {
      component.blobPath = 'test/path.csv';
      const mockResult = { valid: true, issues: [] };
      transportService.validateCsvFile.and.returnValue(of(mockResult));
      
      component.validate();
      
      expect(component.validationResult).toEqual(mockResult);
      expect(component.isLoading).toBe(false);
      expect(component.error).toBeNull();
    });

    it('should handle validation error', () => {
      component.blobPath = 'test/path.csv';
      transportService.validateCsvFile.and.returnValue(throwError(() => ({ error: { error: 'Validation failed' } })));
      
      component.validate();
      
      expect(component.error).toBeTruthy();
      expect(component.isLoading).toBe(false);
    });

    it('should set error if blobPath is empty', () => {
      component.blobPath = '';
      component.validate();
      
      expect(component.error).toBe('Blob path is required');
    });
  });

  describe('getSeverityIcon', () => {
    it('should return error icon for error issues', () => {
      expect(component.getSeverityIcon('Error: Invalid format')).toBe('error');
    });

    it('should return warning icon for warning issues', () => {
      expect(component.getSeverityIcon('Warning: Missing field')).toBe('warning');
    });

    it('should return info icon for other issues', () => {
      expect(component.getSeverityIcon('Info: Check completed')).toBe('info');
    });
  });

  describe('getSeverityColor', () => {
    it('should return warn color for errors', () => {
      expect(component.getSeverityColor('Error: Invalid')).toBe('warn');
    });

    it('should return accent color for warnings', () => {
      expect(component.getSeverityColor('Warning: Issue')).toBe('accent');
    });

    it('should return primary color for info', () => {
      expect(component.getSeverityColor('Info: Message')).toBe('primary');
    });
  });

  describe('edge cases', () => {
    it('should handle validation with delimiter parameter', () => {
      component.blobPath = 'test/path.csv';
      component.delimiter = ',';
      transportService.validateCsvFile.and.returnValue(of({ valid: true }));
      
      component.validate();
      
      expect(transportService.validateCsvFile).toHaveBeenCalledWith('test/path.csv', ',');
    });

    it('should handle case-insensitive severity detection', () => {
      expect(component.getSeverityIcon('ERROR: test')).toBe('error');
      expect(component.getSeverityIcon('WARNING: test')).toBe('warning');
      expect(component.getSeverityIcon('FAILED: test')).toBe('error');
    });

    it('should handle null or undefined validation result', () => {
      component.blobPath = 'test/path.csv';
      transportService.validateCsvFile.and.returnValue(of(null));
      
      component.validate();
      
      expect(component.validationResult).toBeNull();
      expect(component.isLoading).toBe(false);
    });

    it('should handle network timeout error', () => {
      component.blobPath = 'test/path.csv';
      transportService.validateCsvFile.and.returnValue(throwError(() => ({ status: 0, message: 'Timeout' })));
      
      component.validate();
      
      expect(component.error).toBeTruthy();
    });
  });
});
