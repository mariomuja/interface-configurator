import { Injectable } from '@angular/core';
import { Observable, BehaviorSubject } from 'rxjs';
import { TransportService } from './transport.service';

export interface BlobFolder {
  name: string;
  path: string;
  files: BlobFile[];
  totalSize: number;
  fileCount: number;
}

export interface BlobFile {
  name: string;
  fullPath: string;
  size: number;
  lastModified: Date;
  contentType?: string;
}

@Injectable({
  providedIn: 'root'
})
export class BlobContainerService {
  private readonly FILES_PER_PAGE = 10;
  
  private foldersSubject = new BehaviorSubject<BlobFolder[]>([]);
  public folders$ = this.foldersSubject.asObservable();
  
  private selectedFilesSubject = new BehaviorSubject<Set<string>>(new Set());
  public selectedFiles$ = this.selectedFilesSubject.asObservable();
  
  private paginationSubject = new BehaviorSubject<Map<string, { loadedCount: number; hasMore: boolean; isLoadingMore: boolean }>>(new Map());
  public pagination$ = this.paginationSubject.asObservable();

  constructor(
    private transportService: TransportService
  ) {}

  getFolders(): BlobFolder[] {
    return this.foldersSubject.value;
  }

  getSelectedFiles(): Set<string> {
    return this.selectedFilesSubject.value;
  }

  getPagination(folderPath: string): { loadedCount: number; hasMore: boolean; isLoadingMore: boolean } {
    const pagination = this.paginationSubject.value;
    return pagination.get(folderPath) || { loadedCount: 0, hasMore: false, isLoadingMore: false };
  }

  loadFolders(containerName: string, sortBy: 'name' | 'date' | 'size' = 'date', sortOrder: 'asc' | 'desc' = 'desc'): Observable<BlobFolder[]> {
    return new Observable(observer => {
      this.transportService.getBlobContainerFolders(containerName).subscribe({
        next: (folders) => {
          const sortedFolders = this.sortFolders(folders, sortBy, sortOrder);
          this.foldersSubject.next(sortedFolders);
          observer.next(sortedFolders);
          observer.complete();
        },
        error: (error) => {
          console.error('Error loading blob container folders:', error);
          observer.error(error);
        }
      });
    });
  }

  loadMoreFiles(folderPath: string, containerName: string): Observable<BlobFile[]> {
    return new Observable(observer => {
      const pagination = this.getPagination(folderPath);
      if (pagination.isLoadingMore || !pagination.hasMore) {
        observer.next([]);
        observer.complete();
        return;
      }

      // Update pagination state
      const updatedPagination = new Map(this.paginationSubject.value);
      updatedPagination.set(folderPath, { ...pagination, isLoadingMore: true });
      this.paginationSubject.next(updatedPagination);

      // Reload folders with increased maxFiles to get more files
      const currentLoaded = pagination.loadedCount;
      const maxFiles = currentLoaded + this.FILES_PER_PAGE;
      
      this.transportService.getBlobContainerFolders(containerName, folderPath, maxFiles).subscribe({
        next: (folders) => {
          const foldersList = this.sortFolders(folders, 'date', 'desc');
          const folder = foldersList.find(f => f.path === folderPath);
          
          if (folder) {
            const folders = this.getFolders();
            const folderIndex = folders.findIndex(f => f.path === folderPath);
            
            if (folderIndex >= 0) {
              folders[folderIndex] = folder;
              
              // Update pagination
              const newPagination = new Map(this.paginationSubject.value);
              newPagination.set(folderPath, {
                loadedCount: folder.files.length,
                hasMore: folder.files.length >= maxFiles,
                isLoadingMore: false
              });
              this.paginationSubject.next(newPagination);
              
              this.foldersSubject.next([...folders]);
            }
          }
          
          observer.next(folder?.files || []);
          observer.complete();
        },
        error: (error) => {
          // Reset loading state on error
          const errorPagination = new Map(this.paginationSubject.value);
          errorPagination.set(folderPath, { ...pagination, isLoadingMore: false });
          this.paginationSubject.next(errorPagination);
          
          observer.error(error);
        }
      });
    });
  }

  sortFolders(folders: any[], sortBy: 'name' | 'date' | 'size', sortOrder: 'asc' | 'desc'): BlobFolder[] {
    const sorted = [...folders];
    
    sorted.sort((a, b) => {
      let comparison = 0;
      
      switch (sortBy) {
        case 'name':
          comparison = a.name.localeCompare(b.name);
          break;
        case 'date':
          const dateA = a.files && a.files.length > 0 
            ? new Date(a.files[0].lastModified).getTime() 
            : 0;
          const dateB = b.files && b.files.length > 0 
            ? new Date(b.files[0].lastModified).getTime() 
            : 0;
          comparison = dateA - dateB;
          break;
        case 'size':
          comparison = (a.totalSize || 0) - (b.totalSize || 0);
          break;
      }
      
      return sortOrder === 'asc' ? comparison : -comparison;
    });
    
    return sorted;
  }

  toggleFileSelection(fullPath: string): void {
    const selected = new Set(this.getSelectedFiles());
    if (selected.has(fullPath)) {
      selected.delete(fullPath);
    } else {
      selected.add(fullPath);
    }
    this.selectedFilesSubject.next(selected);
  }

  isFileSelected(fullPath: string): boolean {
    return this.getSelectedFiles().has(fullPath);
  }

  selectAllFiles(folder: BlobFolder): void {
    const selected = new Set(this.getSelectedFiles());
    folder.files.forEach(file => selected.add(file.fullPath));
    this.selectedFilesSubject.next(selected);
  }

  deselectAllFiles(): void {
    this.selectedFilesSubject.next(new Set());
  }

  getSelectedFilesCount(): number {
    return this.getSelectedFiles().size;
  }

  areAllFilesInFolderSelected(folder: BlobFolder): boolean {
    if (!folder.files || folder.files.length === 0) {
      return false;
    }
    return folder.files.every(file => this.isFileSelected(file.fullPath));
  }

  areSomeFilesInFolderSelected(folder: BlobFolder): boolean {
    if (!folder.files || folder.files.length === 0) {
      return false;
    }
    return folder.files.some(file => this.isFileSelected(file.fullPath));
  }

  toggleFolderSelection(folder: BlobFolder): void {
    if (this.areAllFilesInFolderSelected(folder)) {
      // Deselect all files in folder
      const selected = new Set(this.getSelectedFiles());
      folder.files.forEach(file => selected.delete(file.fullPath));
      this.selectedFilesSubject.next(selected);
    } else {
      // Select all files in folder
      this.selectAllFiles(folder);
    }
  }

  deleteSelectedFiles(containerName: string): Observable<void> {
    return new Observable(observer => {
      const selectedFiles = Array.from(this.getSelectedFiles());
      if (selectedFiles.length === 0) {
        observer.next();
        observer.complete();
        return;
      }

      // Delete files in parallel
      const deleteOperations = selectedFiles.map(fullPath => 
        this.transportService.deleteBlobFile(containerName, fullPath)
      );

      // Convert Observables to Promises
      Promise.all(deleteOperations.map(op => new Promise((resolve, reject) => {
        op.subscribe({ next: resolve, error: reject });
      }))).then(() => {
        // Clear selection
        this.deselectAllFiles();
        
        // Reload folders
        this.loadFolders(containerName).subscribe({
          next: () => {
            observer.next();
            observer.complete();
          },
          error: (error) => {
            observer.error(error);
          }
        });
      }).catch(error => {
        observer.error(error);
      });
    });
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
  }

  getTotalFileCount(): number {
    return this.getFolders().reduce((total, folder) => total + (folder.fileCount || 0), 0);
  }
}

