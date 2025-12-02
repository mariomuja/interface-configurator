import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SchemaComparisonComponent } from './schema-comparison.component';
import { TransportService } from '../../services/transport.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('SchemaComparisonComponent', () => {
  let component: SchemaComparisonComponent;
  let fixture: ComponentFixture<SchemaComparisonComponent>;
  let transportService: jasmine.SpyObj<TransportService>;

  beforeEach(async () => {
    const transportServiceSpy = jasmine.createSpyObj('TransportService', ['compareCsvSqlSchema']);

    await TestBed.configureTestingModule({
      imports: [
        SchemaComparisonComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: TransportService, useValue: transportServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SchemaComparisonComponent);
    component = fixture.componentInstance;
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit', () => {
    it('should compare schemas if interfaceName and csvBlobPath are provided', () => {
      component.interfaceName = 'TestInterface';
      component.csvBlobPath = 'test/path.csv';
      transportService.compareCsvSqlSchema.and.returnValue(of({ matches: [] }));
      
      component.ngOnInit();
      
      expect(transportService.compareCsvSqlSchema).toHaveBeenCalled();
    });

    it('should not compare if inputs are missing', () => {
      component.interfaceName = '';
      component.csvBlobPath = '';
      component.ngOnInit();
      
      expect(transportService.compareCsvSqlSchema).not.toHaveBeenCalled();
    });
  });

  describe('compare', () => {
    it('should compare schemas successfully', () => {
      component.interfaceName = 'TestInterface';
      component.csvBlobPath = 'test/path.csv';
      const mockResult = { matches: [], mismatches: [] };
      transportService.compareCsvSqlSchema.and.returnValue(of(mockResult));
      
      component.compare();
      
      expect(component.comparisonResult).toEqual(mockResult);
      expect(component.isLoading).toBe(false);
      expect(component.error).toBeNull();
    });

    it('should handle comparison error', () => {
      component.interfaceName = 'TestInterface';
      component.csvBlobPath = 'test/path.csv';
      transportService.compareCsvSqlSchema.and.returnValue(throwError(() => ({ error: { error: 'Comparison failed' } })));
      
      component.compare();
      
      expect(component.error).toBeTruthy();
      expect(component.isLoading).toBe(false);
    });

    it('should set error if required inputs are missing', () => {
      component.interfaceName = '';
      component.csvBlobPath = '';
      component.compare();
      
      expect(component.error).toBe('Interface name and CSV blob path are required');
    });
  });

  describe('getTypeMismatchDataSource', () => {
    it('should return empty array if no mismatches', () => {
      component.comparisonResult = { typeMismatches: [] };
      expect(component.getTypeMismatchDataSource().length).toBe(0);
    });

    it('should return empty array if comparisonResult is null', () => {
      component.comparisonResult = null;
      expect(component.getTypeMismatchDataSource().length).toBe(0);
    });

    it('should return formatted mismatches', () => {
      component.comparisonResult = {
        typeMismatches: [
          {
            columnName: 'TestColumn',
            csvType: 'string',
            sqlType: 'int'
          }
        ]
      };
      
      const result = component.getTypeMismatchDataSource();
      expect(result.length).toBe(1);
      expect(result[0].columnName).toBe('TestColumn');
    });

    it('should handle mismatches with all fields', () => {
      component.comparisonResult = {
        typeMismatches: [
          {
            columnName: 'TestColumn',
            csvType: 'string',
            sqlType: 'int',
            csvSqlTypeDefinition: 'VARCHAR(255)',
            sqlSqlTypeDefinition: 'INT'
          }
        ]
      };
      
      const result = component.getTypeMismatchDataSource();
      expect(result[0].csvSqlTypeDefinition).toBe('VARCHAR(255)');
      expect(result[0].sqlSqlTypeDefinition).toBe('INT');
    });
  });

  describe('edge cases', () => {
    it('should handle comparison with custom table name', () => {
      component.interfaceName = 'TestInterface';
      component.csvBlobPath = 'test/path.csv';
      component.tableName = 'CustomTable';
      transportService.compareCsvSqlSchema.and.returnValue(of({ matches: [] }));
      
      component.compare();
      
      expect(transportService.compareCsvSqlSchema).toHaveBeenCalledWith(
        'TestInterface',
        'test/path.csv',
        'CustomTable'
      );
    });

    it('should handle partial input - only interfaceName', () => {
      component.interfaceName = 'TestInterface';
      component.csvBlobPath = '';
      component.compare();
      
      expect(component.error).toBe('Interface name and CSV blob path are required');
    });

    it('should handle partial input - only csvBlobPath', () => {
      component.interfaceName = '';
      component.csvBlobPath = 'test/path.csv';
      component.compare();
      
      expect(component.error).toBe('Interface name and CSV blob path are required');
    });

    it('should handle HTTP 404 error', () => {
      component.interfaceName = 'TestInterface';
      component.csvBlobPath = 'test/path.csv';
      transportService.compareCsvSqlSchema.and.returnValue(throwError(() => ({ status: 404, error: { error: 'Not found' } })));
      
      component.compare();
      
      expect(component.error).toBeTruthy();
    });
  });
});
