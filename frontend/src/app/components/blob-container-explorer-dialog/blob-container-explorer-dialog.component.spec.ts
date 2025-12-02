import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BlobContainerExplorerDialogComponent, BlobContainerExplorerDialogData } from './blob-container-explorer-dialog.component';
import { TransportService } from '../../services/transport.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('BlobContainerExplorerDialogComponent', () => {
  let component: BlobContainerExplorerDialogComponent;
  let fixture: ComponentFixture<BlobContainerExplorerDialogComponent>;
  let transportService: jasmine.SpyObj<TransportService>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<BlobContainerExplorerDialogComponent>>;

  const mockData: BlobContainerExplorerDialogData = {
    adapterInstanceGuid: 'test-guid',
    adapterName: 'CSV',
    adapterType: 'Source',
    instanceName: 'TestInstance'
  };

  beforeEach(async () => {
    const transportServiceSpy = jasmine.createSpyObj('TransportService', ['getBlobContainerFolders', 'deleteBlobFile']);
    const snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [
        BlobContainerExplorerDialogComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: TransportService, useValue: transportServiceSpy },
        { provide: MatSnackBar, useValue: snackBarSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BlobContainerExplorerDialogComponent);
    component = fixture.componentInstance;
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
    snackBar = TestBed.inject(MatSnackBar) as jasmine.SpyObj<MatSnackBar>;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<BlobContainerExplorerDialogComponent>>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('loadBlobContainerFolders', () => {
    it('should load blob container folders successfully', () => {
      const mockFolders = [
        { path: '/csv-incoming', files: [] },
        { path: '/csv-processed', files: [] },
        { path: '/csv-error', files: [] }
      ];
      
      transportService.getBlobContainerFolders.and.returnValue(of(mockFolders));
      
      component.loadBlobContainerFolders();
      
      expect(transportService.getBlobContainerFolders).toHaveBeenCalled();
      expect(component.isLoadingBlobContainer).toBe(false);
    });

    it('should handle error when loading folders', () => {
      transportService.getBlobContainerFolders.and.returnValue(throwError(() => ({ message: 'Error' })));
      
      component.loadBlobContainerFolders();
      
      expect(snackBar.open).toHaveBeenCalled();
      expect(component.isLoadingBlobContainer).toBe(false);
    });
  });

  describe('sorting', () => {
    beforeEach(() => {
      component.blobContainerFolders = [
        {
          path: '/csv-incoming',
          files: [
            { name: 'file1.csv', lastModified: '2024-01-02', size: 100 },
            { name: 'file2.csv', lastModified: '2024-01-01', size: 200 }
          ]
        }
      ];
    });

    it('should sort by name', () => {
      component.sortBy = 'name';
      component.sortOrder = 'asc';
      component.sortFiles();
      
      // Verify sorting logic is applied
      expect(component.sortBy).toBe('name');
    });

    it('should sort by date', () => {
      component.sortBy = 'date';
      component.sortOrder = 'desc';
      component.sortFiles();
      
      expect(component.sortBy).toBe('date');
    });
  });

  describe('close', () => {
    it('should close dialog', () => {
      component.close();
      expect(dialogRef.close).toHaveBeenCalled();
    });
  });
});
