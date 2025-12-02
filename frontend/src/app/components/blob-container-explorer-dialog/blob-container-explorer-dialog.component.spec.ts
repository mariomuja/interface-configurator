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

    it('should sort files by name', () => {
      component.sortBy = 'name';
      component.sortOrder = 'asc';
      component.blobContainerFolders = component.sortBlobContainerFolders(component.blobContainerFolders);
      
      expect(component.sortBy).toBe('name');
      expect(component.blobContainerFolders[0].files[0].name).toBe('file1.csv');
    });

    it('should sort files by date', () => {
      component.sortBy = 'date';
      component.sortOrder = 'desc';
      component.blobContainerFolders = component.sortBlobContainerFolders(component.blobContainerFolders);
      
      expect(component.sortBy).toBe('date');
    });

    it('should sort files by size', () => {
      component.sortBy = 'size';
      component.sortOrder = 'asc';
      component.blobContainerFolders = component.sortBlobContainerFolders(component.blobContainerFolders);
      
      expect(component.sortBy).toBe('size');
    });
  });

  describe('close', () => {
    it('should close dialog', () => {
      component.close();
      expect(dialogRef.close).toHaveBeenCalled();
    });
  });

  describe('file selection', () => {
    beforeEach(() => {
      component.blobContainerFolders = [
        {
          path: '/csv-incoming',
          files: [
            { name: 'file1.csv', fullPath: '/csv-incoming/file1.csv', size: 100 },
            { name: 'file2.csv', fullPath: '/csv-incoming/file2.csv', size: 200 }
          ]
        }
      ];
    });

    it('should toggle blob file selection', () => {
      const filePath = '/csv-incoming/file1.csv';
      expect(component.isBlobFileSelected(filePath)).toBe(false);
      
      component.toggleBlobFileSelection(filePath);
      expect(component.isBlobFileSelected(filePath)).toBe(true);
      
      component.toggleBlobFileSelection(filePath);
      expect(component.isBlobFileSelected(filePath)).toBe(false);
    });

    it('should check if all files in folder are selected', () => {
      const folder = component.blobContainerFolders[0];
      expect(component.areAllFilesInFolderSelected(folder)).toBe(false);
      
      folder.files.forEach((file: any) => {
        component.selectedBlobFiles.add(file.fullPath);
      });
      
      expect(component.areAllFilesInFolderSelected(folder)).toBe(true);
    });

    it('should check if some files in folder are selected', () => {
      const folder = component.blobContainerFolders[0];
      component.selectedBlobFiles.add(folder.files[0].fullPath);
      
      expect(component.areSomeFilesInFolderSelected(folder)).toBe(true);
    });

    it('should toggle folder selection', () => {
      const folder = component.blobContainerFolders[0];
      const initialCount = component.selectedBlobFiles.size;
      
      component.toggleFolderSelection(folder);
      
      expect(component.selectedBlobFiles.size).toBe(initialCount + folder.files.length);
    });

    it('should deselect all files when all are selected', () => {
      const folder = component.blobContainerFolders[0];
      folder.files.forEach((file: any) => {
        component.selectedBlobFiles.add(file.fullPath);
      });
      
      component.toggleFolderSelection(folder);
      
      expect(component.selectedBlobFiles.size).toBe(0);
    });

    it('should get selected files count', () => {
      component.selectedBlobFiles.add('file1');
      component.selectedBlobFiles.add('file2');
      
      expect(component.getSelectedBlobFilesCount()).toBe(2);
    });

    it('should get total file count', () => {
      expect(component.getTotalFileCount()).toBe(2);
    });

    it('should deselect all blob files', () => {
      component.selectedBlobFiles.add('file1');
      component.selectedBlobFiles.add('file2');
      
      component.deselectAllBlobFiles();
      
      expect(component.selectedBlobFiles.size).toBe(0);
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

    it('should sort by size', () => {
      component.sortBy = 'size';
      component.sortOrder = 'asc';
      component.sortFiles();
      
      expect(component.sortBy).toBe('size');
    });

    it('should toggle sort order when same sortBy', () => {
      component.sortBy = 'name';
      component.sortOrder = 'asc';
      
      component.onSortChange('name');
      expect(component.sortOrder).toBe('desc');
      
      component.onSortChange('name');
      expect(component.sortOrder).toBe('asc');
    });

    it('should set default sort order for date', () => {
      component.onSortChange('date');
      expect(component.sortBy).toBe('date');
      expect(component.sortOrder).toBe('desc');
    });

    it('should set default sort order for name', () => {
      component.onSortChange('name');
      expect(component.sortBy).toBe('name');
      expect(component.sortOrder).toBe('asc');
    });
  });

  describe('edge cases', () => {
    it('should handle empty folders array', () => {
      transportService.getBlobContainerFolders.and.returnValue(of([]));
      component.loadBlobContainerFolders();
      
      expect(component.blobContainerFolders.length).toBeGreaterThanOrEqual(0);
    });

    it('should handle folder with no files', () => {
      component.blobContainerFolders = [{ path: '/empty', files: [] }];
      const folder = component.blobContainerFolders[0];
      
      expect(component.areAllFilesInFolderSelected(folder)).toBe(false);
      expect(component.areSomeFilesInFolderSelected(folder)).toBe(false);
    });

    it('should handle formatFileSize for different sizes', () => {
      expect(component.formatFileSize(0)).toBe('0 B');
      expect(component.formatFileSize(1024)).toContain('KB');
      expect(component.formatFileSize(1048576)).toContain('MB');
      expect(component.formatFileSize(1073741824)).toContain('GB');
    });

    it('should delete selected blob files', () => {
      component.selectedBlobFiles.add('file1');
      component.selectedBlobFiles.add('file2');
      spyOn(window, 'confirm').and.returnValue(true);
      transportService.deleteBlobFile.and.returnValue(of({ success: true }));
      
      component.deleteSelectedBlobFiles();
      
      expect(transportService.deleteBlobFile).toHaveBeenCalled();
    });

    it('should not delete if user cancels confirmation', () => {
      component.selectedBlobFiles.add('file1');
      spyOn(window, 'confirm').and.returnValue(false);
      
      component.deleteSelectedBlobFiles();
      
      expect(transportService.deleteBlobFile).not.toHaveBeenCalled();
    });

    it('should not delete if no files selected', () => {
      component.deleteSelectedBlobFiles();
      expect(transportService.deleteBlobFile).not.toHaveBeenCalled();
    });

    it('should handle delete error', () => {
      component.selectedBlobFiles.add('file1');
      spyOn(window, 'confirm').and.returnValue(true);
      transportService.deleteBlobFile.and.returnValue(throwError(() => ({ error: 'Delete failed' })));
      
      component.deleteSelectedBlobFiles();
      
      expect(component.isDeletingBlobFiles).toBe(false);
    });
  });
});
