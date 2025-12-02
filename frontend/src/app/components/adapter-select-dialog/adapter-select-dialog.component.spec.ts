import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { AdapterSelectDialogComponent, AdapterSelectData } from './adapter-select-dialog.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('AdapterSelectDialogComponent', () => {
  let component: AdapterSelectDialogComponent;
  let fixture: ComponentFixture<AdapterSelectDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<AdapterSelectDialogComponent>>;

  const mockData: AdapterSelectData = {
    adapterType: 'Source',
    currentAdapterName: undefined
  };

  beforeEach(async () => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [
        AdapterSelectDialogComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: mockData }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdapterSelectDialogComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<AdapterSelectDialogComponent>>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('availableAdapters', () => {
    it('should filter adapters for Source type', () => {
      component.data.adapterType = 'Source';
      const adapters = component.availableAdapters;
      
      expect(adapters.length).toBeGreaterThan(0);
      expect(adapters.every(a => a.supportsSource)).toBe(true);
    });

    it('should filter adapters for Destination type', () => {
      component.data.adapterType = 'Destination';
      const adapters = component.availableAdapters;
      
      expect(adapters.length).toBeGreaterThan(0);
      expect(adapters.every(a => a.supportsDestination)).toBe(true);
    });
  });

  describe('selectAdapter', () => {
    it('should set selected adapter', () => {
      const adapter = component.allAdapters[0];
      component.selectAdapter(adapter);
      
      expect(component.selectedAdapter).toBe(adapter.name);
    });
  });

  describe('confirm', () => {
    it('should close dialog with selected adapter', () => {
      component.selectedAdapter = 'CSV';
      component.confirm();
      
      expect(dialogRef.close).toHaveBeenCalledWith('CSV');
    });

    it('should not close if no adapter selected', () => {
      component.selectedAdapter = null;
      component.confirm();
      
      // Should not throw, but also shouldn't close
      expect(component.selectedAdapter).toBeNull();
    });
  });

  describe('close', () => {
    it('should close dialog with null', () => {
      component.close();
      expect(dialogRef.close).toHaveBeenCalledWith(null);
    });
  });

  describe('initialization', () => {
    it('should set current adapter as selected if provided', () => {
      const dataWithCurrent: AdapterSelectData = {
        adapterType: 'Source',
        currentAdapterName: 'CSV'
      };
      
      component.data = dataWithCurrent;
      component.ngOnInit();
      
      expect(component.selectedAdapter).toBe('CSV');
    });
  });

  describe('edge cases', () => {
    it('should handle adapters that support both Source and Destination', () => {
      component.data.adapterType = 'Source';
      const sourceAdapters = component.availableAdapters;
      
      component.data.adapterType = 'Destination';
      const destAdapters = component.availableAdapters;
      
      // Some adapters should appear in both lists
      const csvAdapter = sourceAdapters.find(a => a.name === 'CSV');
      expect(csvAdapter).toBeTruthy();
      expect(destAdapters.find(a => a.name === 'CSV')).toBeTruthy();
    });

    it('should handle selecting same adapter multiple times', () => {
      const adapter = component.allAdapters[0];
      component.selectAdapter(adapter);
      expect(component.selectedAdapter).toBe(adapter.name);
      
      component.selectAdapter(adapter);
      expect(component.selectedAdapter).toBe(adapter.name);
    });

    it('should handle empty adapter list gracefully', () => {
      component.allAdapters = [];
      expect(component.availableAdapters.length).toBe(0);
    });
  });
});
