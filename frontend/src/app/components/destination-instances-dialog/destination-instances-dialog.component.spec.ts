import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { DestinationInstancesDialogComponent, DestinationInstancesDialogData, DestinationAdapterInstance } from './destination-instances-dialog.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('DestinationInstancesDialogComponent', () => {
  let component: DestinationInstancesDialogComponent;
  let fixture: ComponentFixture<DestinationInstancesDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<DestinationInstancesDialogComponent>>;

  const mockData: DestinationInstancesDialogData = {
    instances: [],
    availableAdapters: [
      { name: 'CSV', displayName: 'CSV', icon: 'description' },
      { name: 'SqlServer', displayName: 'SQL Server', icon: 'storage' }
    ],
    hasSourceAdapter: true
  };

  beforeEach(async () => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [
        DestinationInstancesDialogComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: mockData }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DestinationInstancesDialogComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<DestinationInstancesDialogComponent>>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit', () => {
    it('should initialize instances from data', () => {
      const instances: DestinationAdapterInstance[] = [
        {
          adapterInstanceGuid: 'guid-1',
          instanceName: 'Instance1',
          adapterName: 'CSV',
          isEnabled: true
        }
      ];
      
      component.data.instances = instances;
      component.ngOnInit();
      
      expect(component.instances.length).toBe(1);
      expect(component.instances[0].instanceName).toBe('Instance1');
    });

    it('should use default adapters if not provided', () => {
      component.data.availableAdapters = [];
      component.ngOnInit();
      
      expect(component.availableAdapters.length).toBeGreaterThan(0);
    });
  });

  describe('onAddAdapter', () => {
    it('should add new adapter instance when source adapter exists', () => {
      component.data.hasSourceAdapter = true;
      const initialCount = component.instances.length;
      
      component.onAddAdapter('CSV');
      
      expect(component.instances.length).toBe(initialCount + 1);
      expect(component.instances[component.instances.length - 1].adapterName).toBe('CSV');
    });

    it('should not add adapter when source adapter does not exist', () => {
      component.data.hasSourceAdapter = false;
      const initialCount = component.instances.length;
      
      spyOn(window, 'alert');
      component.onAddAdapter('CSV');
      
      expect(component.instances.length).toBe(initialCount);
      expect(window.alert).toHaveBeenCalled();
    });

    it('should set default configuration for SqlServer', () => {
      component.data.hasSourceAdapter = true;
      component.onAddAdapter('SqlServer');
      
      const newInstance = component.instances[component.instances.length - 1];
      expect(newInstance.configuration.tableName).toBe('TransportData');
    });
  });

  describe('onRemoveAdapter', () => {
    it('should remove adapter instance', () => {
      const instance: DestinationAdapterInstance = {
        adapterInstanceGuid: 'guid-1',
        instanceName: 'Instance1',
        adapterName: 'CSV',
        isEnabled: true
      };
      
      component.instances = [instance];
      component.onRemoveAdapter('guid-1');
      
      expect(component.instances.length).toBe(0);
    });
  });

  describe('onSave', () => {
    it('should close dialog with instances', () => {
      component.instances = [
        {
          adapterInstanceGuid: 'guid-1',
          instanceName: 'Instance1',
          adapterName: 'CSV',
          isEnabled: true
        }
      ];
      
      component.onSave();
      
      expect(dialogRef.close).toHaveBeenCalledWith(component.instances);
    });
  });

  describe('onCancel', () => {
    it('should close dialog without data', () => {
      component.onCancel();
      expect(dialogRef.close).toHaveBeenCalled();
    });
  });

  describe('generateGuid', () => {
    it('should generate valid UUID', () => {
      const guid = component.generateGuid();
      const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
      expect(guid).toMatch(uuidRegex);
    });

    it('should generate unique GUIDs', () => {
      const guid1 = component.generateGuid();
      const guid2 = component.generateGuid();
      expect(guid1).not.toBe(guid2);
    });
  });

  describe('edge cases', () => {
    it('should handle removing non-existent adapter gracefully', () => {
      component.instances = [
        {
          adapterInstanceGuid: 'guid-1',
          instanceName: 'Instance1',
          adapterName: 'CSV',
          isEnabled: true
        }
      ];
      
      const initialCount = component.instances.length;
      component.onRemoveAdapter('non-existent-guid');
      
      expect(component.instances.length).toBe(initialCount);
    });

    it('should handle adding multiple adapters of same type', () => {
      component.data.hasSourceAdapter = true;
      
      component.onAddAdapter('CSV');
      component.onAddAdapter('CSV');
      
      expect(component.instances.length).toBe(2);
      expect(component.instances.every(i => i.adapterName === 'CSV')).toBe(true);
    });

    it('should handle empty instances list on save', () => {
      component.instances = [];
      component.onSave();
      
      expect(dialogRef.close).toHaveBeenCalledWith([]);
    });

    it('should handle getDefaultInstanceName for different adapters', () => {
      component.data.hasSourceAdapter = true;
      component.onAddAdapter('CSV');
      component.onAddAdapter('SqlServer');
      
      expect(component.instances[0].instanceName).toContain('CSV');
      expect(component.instances[1].instanceName).toContain('SqlServer');
    });
  });
});
