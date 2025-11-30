import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { BlobContainerService, BlobFolder, BlobFile } from './blob-container.service';
import { TransportService } from './transport.service';

describe('BlobContainerService', () => {
  let service: BlobContainerService;
  let transportService: jasmine.SpyObj<TransportService>;

  beforeEach(() => {
    const transportSpy = jasmine.createSpyObj('TransportService', [
      'getBlobContainerFolders',
      'deleteBlobFile'
    ]);

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        BlobContainerService,
        { provide: TransportService, useValue: transportSpy }
      ]
    });
    service = TestBed.inject(BlobContainerService);
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should load folders', (done) => {
    const mockFolders: any[] = [
      {
        name: 'csv-incoming',
        path: 'csv-incoming',
        files: [
          { name: 'file1.csv', fullPath: 'csv-incoming/file1.csv', size: 1000, lastModified: new Date() }
        ],
        totalSize: 1000,
        fileCount: 1
      }
    ];

    transportService.getBlobContainerFolders.and.returnValue(of(mockFolders));

    service.loadFolders('csv-files', 'date', 'desc').subscribe(folders => {
      expect(folders.length).toBe(1);
      expect(folders[0].name).toBe('csv-incoming');
      expect(service.getFolders()).toEqual(folders);
      done();
    });
  });

  it('should sort folders by name', (done) => {
    const mockFolders: any[] = [
      { name: 'B-folder', path: 'B-folder', files: [], totalSize: 0, fileCount: 0 },
      { name: 'A-folder', path: 'A-folder', files: [], totalSize: 0, fileCount: 0 }
    ];

    transportService.getBlobContainerFolders.and.returnValue(of(mockFolders));

    service.loadFolders('csv-files', 'name', 'asc').subscribe(folders => {
      expect(folders[0].name).toBe('A-folder');
      expect(folders[1].name).toBe('B-folder');
      done();
    });
  });

  it('should toggle file selection', () => {
    const filePath = 'csv-incoming/test.csv';
    expect(service.isFileSelected(filePath)).toBeFalsy();
    
    service.toggleFileSelection(filePath);
    expect(service.isFileSelected(filePath)).toBeTruthy();
    
    service.toggleFileSelection(filePath);
    expect(service.isFileSelected(filePath)).toBeFalsy();
  });

  it('should select all files in folder', () => {
    const folder: BlobFolder = {
      name: 'test',
      path: 'test',
      files: [
        { name: 'file1.csv', fullPath: 'test/file1.csv', size: 100, lastModified: new Date() },
        { name: 'file2.csv', fullPath: 'test/file2.csv', size: 200, lastModified: new Date() }
      ],
      totalSize: 300,
      fileCount: 2
    };

    service.selectAllFiles(folder);
    expect(service.getSelectedFilesCount()).toBe(2);
    expect(service.isFileSelected('test/file1.csv')).toBeTruthy();
    expect(service.isFileSelected('test/file2.csv')).toBeTruthy();
  });

  it('should deselect all files', () => {
    const folder: BlobFolder = {
      name: 'test',
      path: 'test',
      files: [
        { name: 'file1.csv', fullPath: 'test/file1.csv', size: 100, lastModified: new Date() }
      ],
      totalSize: 100,
      fileCount: 1
    };

    service.selectAllFiles(folder);
    expect(service.getSelectedFilesCount()).toBe(1);
    
    service.deselectAllFiles();
    expect(service.getSelectedFilesCount()).toBe(0);
  });

  it('should check if all files in folder are selected', () => {
    const folder: BlobFolder = {
      name: 'test',
      path: 'test',
      files: [
        { name: 'file1.csv', fullPath: 'test/file1.csv', size: 100, lastModified: new Date() },
        { name: 'file2.csv', fullPath: 'test/file2.csv', size: 200, lastModified: new Date() }
      ],
      totalSize: 300,
      fileCount: 2
    };

    expect(service.areAllFilesInFolderSelected(folder)).toBeFalsy();
    
    service.selectAllFiles(folder);
    expect(service.areAllFilesInFolderSelected(folder)).toBeTruthy();
  });

  it('should check if some files in folder are selected', () => {
    const folder: BlobFolder = {
      name: 'test',
      path: 'test',
      files: [
        { name: 'file1.csv', fullPath: 'test/file1.csv', size: 100, lastModified: new Date() },
        { name: 'file2.csv', fullPath: 'test/file2.csv', size: 200, lastModified: new Date() }
      ],
      totalSize: 300,
      fileCount: 2
    };

    expect(service.areSomeFilesInFolderSelected(folder)).toBeFalsy();
    
    service.toggleFileSelection('test/file1.csv');
    expect(service.areSomeFilesInFolderSelected(folder)).toBeTruthy();
  });

  it('should toggle folder selection', () => {
    const folder: BlobFolder = {
      name: 'test',
      path: 'test',
      files: [
        { name: 'file1.csv', fullPath: 'test/file1.csv', size: 100, lastModified: new Date() }
      ],
      totalSize: 100,
      fileCount: 1
    };

    service.toggleFolderSelection(folder);
    expect(service.areAllFilesInFolderSelected(folder)).toBeTruthy();
    
    service.toggleFolderSelection(folder);
    expect(service.areAllFilesInFolderSelected(folder)).toBeFalsy();
  });

  it('should format file size', () => {
    expect(service.formatFileSize(0)).toBe('0 B');
    expect(service.formatFileSize(1024)).toBe('1 KB');
    expect(service.formatFileSize(1048576)).toBe('1 MB');
    expect(service.formatFileSize(1073741824)).toBe('1 GB');
  });

  it('should get total file count', () => {
    const folders: BlobFolder[] = [
      { name: 'folder1', path: 'folder1', files: [], totalSize: 0, fileCount: 5 },
      { name: 'folder2', path: 'folder2', files: [], totalSize: 0, fileCount: 3 }
    ];
    service['foldersSubject'].next(folders);
    
    expect(service.getTotalFileCount()).toBe(8);
  });

  it('should delete selected files', (done) => {
    const folder: BlobFolder = {
      name: 'test',
      path: 'test',
      files: [
        { name: 'file1.csv', fullPath: 'test/file1.csv', size: 100, lastModified: new Date() }
      ],
      totalSize: 100,
      fileCount: 1
    };

    service.selectAllFiles(folder);
    service['foldersSubject'].next([folder]);
    
    transportService.deleteBlobFile.and.returnValue(of({}));
    transportService.getBlobContainerFolders.and.returnValue(of([]));

    service.deleteSelectedFiles('csv-files').subscribe(() => {
      expect(service.getSelectedFilesCount()).toBe(0);
      done();
    });
  });

  it('should handle errors when loading folders', (done) => {
    transportService.getBlobContainerFolders.and.returnValue(throwError(() => new Error('Test error')));

    service.loadFolders('csv-files').subscribe({
      next: () => fail('Should have thrown error'),
      error: (error) => {
        expect(error).toBeTruthy();
        done();
      }
    });
  });

  it('should get pagination state', () => {
    const pagination = service.getPagination('test-folder');
    expect(pagination.loadedCount).toBe(0);
    expect(pagination.hasMore).toBeFalsy();
    expect(pagination.isLoadingMore).toBeFalsy();
  });
});

