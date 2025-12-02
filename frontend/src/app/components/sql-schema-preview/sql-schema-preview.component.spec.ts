import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SqlSchemaPreviewComponent } from './sql-schema-preview.component';
import { TransportService } from '../../services/transport.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('SqlSchemaPreviewComponent', () => {
  let component: SqlSchemaPreviewComponent;
  let fixture: ComponentFixture<SqlSchemaPreviewComponent>;
  let transportService: jasmine.SpyObj<TransportService>;

  beforeEach(async () => {
    const transportServiceSpy = jasmine.createSpyObj('TransportService', ['getSqlTableSchema']);

    await TestBed.configureTestingModule({
      imports: [
        SqlSchemaPreviewComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: TransportService, useValue: transportServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SqlSchemaPreviewComponent);
    component = fixture.componentInstance;
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit', () => {
    it('should load schema if interfaceName is provided', () => {
      component.interfaceName = 'TestInterface';
      transportService.getSqlTableSchema.and.returnValue(of({ columns: [] }));
      
      component.ngOnInit();
      
      expect(transportService.getSqlTableSchema).toHaveBeenCalled();
    });

    it('should not load schema if interfaceName is empty', () => {
      component.interfaceName = '';
      component.ngOnInit();
      
      expect(transportService.getSqlTableSchema).not.toHaveBeenCalled();
    });
  });

  describe('loadSchema', () => {
    it('should load schema successfully', () => {
      component.interfaceName = 'TestInterface';
      const mockSchema = { columns: [{ name: 'TestColumn', dataType: 'varchar' }] };
      transportService.getSqlTableSchema.and.returnValue(of(mockSchema));
      
      component.loadSchema();
      
      expect(component.schema).toEqual(mockSchema);
      expect(component.isLoading).toBe(false);
      expect(component.error).toBeNull();
    });

    it('should handle load error', () => {
      component.interfaceName = 'TestInterface';
      transportService.getSqlTableSchema.and.returnValue(throwError(() => ({ error: { error: 'Load failed' } })));
      
      component.loadSchema();
      
      expect(component.error).toBeTruthy();
      expect(component.isLoading).toBe(false);
    });

    it('should set error if interfaceName is missing', () => {
      component.interfaceName = '';
      component.loadSchema();
      
      expect(component.error).toBe('Interface name is required');
    });
  });

  describe('getDataTypeColor', () => {
    it('should return primary for INT types', () => {
      expect(component.getDataTypeColor('INT')).toBe('primary');
      expect(component.getDataTypeColor('BIGINT')).toBe('primary');
    });

    it('should return accent for VARCHAR types', () => {
      expect(component.getDataTypeColor('VARCHAR')).toBe('accent');
      expect(component.getDataTypeColor('CHAR')).toBe('accent');
    });

    it('should return warn for DATE types', () => {
      expect(component.getDataTypeColor('DATE')).toBe('warn');
      expect(component.getDataTypeColor('DATETIME')).toBe('warn');
    });

    it('should return primary for DECIMAL types', () => {
      expect(component.getDataTypeColor('DECIMAL')).toBe('primary');
      expect(component.getDataTypeColor('FLOAT')).toBe('primary');
    });
  });
});
