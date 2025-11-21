import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { TransportService } from '../../services/transport.service';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

export interface BlobContainerExplorerDialogData {
  adapterInstanceGuid: string;
  adapterName: string;
  adapterType: 'Source' | 'Destination';
  instanceName: string;
}

@Component({
  selector: 'app-blob-container-explorer-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ],
  templateUrl: './blob-container-explorer-dialog.component.html',
  styleUrl: './blob-container-explorer-dialog.component.css'
})
export class BlobContainerExplorerDialogComponent implements OnInit {
  blobContainerFolders: any[] = [];
  isLoadingBlobContainer = false;
  sortBy: 'name' | 'date' | 'size' = 'date';
  sortOrder: 'asc' | 'desc' = 'desc';
  selectedBlobFiles = new Set<string>();
  isDeletingBlobFiles = false;

  constructor(
    public dialogRef: MatDialogRef<BlobContainerExplorerDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: BlobContainerExplorerDialogData,
    private transportService: TransportService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadBlobContainerFolders();
  }

  loadBlobContainerFolders(): void {
    this.isLoadingBlobContainer = true;
    
    // Load all three CSV folders: csv-incoming, csv-processed, csv-error
    this.transportService.getBlobContainerFolders('csv-files', '', 10).pipe(
      catchError(error => {
        console.error('Error loading blob container folders:', error);
        this.snackBar.open('Error loading blob container: ' + (error.error?.message || error.message), 'OK', {
          duration: 5000,
          panelClass: ['error-snackbar']
        });
        return of([]);
      })
    ).subscribe({
      next: (folders) => {
        // Filter to show only the 3 CSV folders
        const requiredFolders = ['/csv-incoming', '/csv-processed', '/csv-error'];
        const folderMap = new Map<string, any>();
        
        // Add existing folders from API response
        (folders || []).forEach((folder: any) => {
          folderMap.set(folder.path, folder);
        });
        
        // Ensure all required folders exist (create empty ones if missing)
        requiredFolders.forEach(folderPath => {
          if (!folderMap.has(folderPath)) {
            folderMap.set(folderPath, {
              path: folderPath,
              files: [],
              totalFileCount: 0,
              hasMoreFiles: false
            });
          }
        });
        
        // Convert map back to array and sort
        const allFolders = Array.from(folderMap.values());
        
        // Sort folders: csv-incoming first, then csv-processed, then csv-error
        const sortedFolders = allFolders.sort((a, b) => {
          const order = ['/csv-incoming', '/csv-processed', '/csv-error'];
          const aIndex = order.indexOf(a.path);
          const bIndex = order.indexOf(b.path);
          if (aIndex !== -1 && bIndex !== -1) return aIndex - bIndex;
          if (aIndex !== -1) return -1;
          if (bIndex !== -1) return 1;
          return a.path.localeCompare(b.path);
        });
        
        this.blobContainerFolders = this.sortBlobContainerFolders(sortedFolders);
        this.isLoadingBlobContainer = false;
      },
      error: (error) => {
        console.error('Error loading blob container folders:', error);
        this.isLoadingBlobContainer = false;
        this.blobContainerFolders = [];
      }
    });
  }

  sortBlobContainerFolders(folders: any[]): any[] {
    return folders.map(folder => {
      const sortedFiles = [...(folder.files || [])].sort((a, b) => {
        let comparison = 0;
        switch (this.sortBy) {
          case 'name':
            comparison = (a.name || '').localeCompare(b.name || '');
            break;
          case 'size':
            comparison = (a.size || 0) - (b.size || 0);
            break;
          case 'date':
            const dateA = new Date(a.lastModified || 0).getTime();
            const dateB = new Date(b.lastModified || 0).getTime();
            comparison = dateA - dateB;
            break;
        }
        return this.sortOrder === 'asc' ? comparison : -comparison;
      });
      return { ...folder, files: sortedFiles };
    });
  }

  onSortChange(sortBy: 'name' | 'date' | 'size'): void {
    if (this.sortBy === sortBy) {
      this.sortOrder = this.sortOrder === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = sortBy;
      this.sortOrder = sortBy === 'date' ? 'desc' : 'asc';
    }
    this.blobContainerFolders = this.sortBlobContainerFolders(this.blobContainerFolders);
  }

  toggleBlobFileSelection(filePath: string): void {
    if (this.selectedBlobFiles.has(filePath)) {
      this.selectedBlobFiles.delete(filePath);
    } else {
      this.selectedBlobFiles.add(filePath);
    }
  }

  isBlobFileSelected(filePath: string): boolean {
    return this.selectedBlobFiles.has(filePath);
  }

  areAllFilesInFolderSelected(folder: any): boolean {
    if (!folder.files || folder.files.length === 0) return false;
    return folder.files.every((file: any) => this.selectedBlobFiles.has(file.fullPath));
  }

  areSomeFilesInFolderSelected(folder: any): boolean {
    if (!folder.files || folder.files.length === 0) return false;
    return folder.files.some((file: any) => this.selectedBlobFiles.has(file.fullPath)) && 
           !this.areAllFilesInFolderSelected(folder);
  }

  toggleFolderSelection(folder: any): void {
    if (!folder.files || folder.files.length === 0) return;
    
    const allSelected = this.areAllFilesInFolderSelected(folder);
    folder.files.forEach((file: any) => {
      if (allSelected) {
        this.selectedBlobFiles.delete(file.fullPath);
      } else {
        this.selectedBlobFiles.add(file.fullPath);
      }
    });
  }

  getSelectedBlobFilesCount(): number {
    return this.selectedBlobFiles.size;
  }

  getTotalFileCount(): number {
    return this.blobContainerFolders.reduce((total, folder) => total + (folder.files?.length || 0), 0);
  }

  deselectAllBlobFiles(): void {
    this.selectedBlobFiles.clear();
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
  }

  deleteSelectedBlobFiles(): void {
    if (this.selectedBlobFiles.size === 0) return;
    
    if (!confirm(`Are you sure you want to delete ${this.selectedBlobFiles.size} file(s)?`)) {
      return;
    }

    this.isDeletingBlobFiles = true;
    const filesToDelete = Array.from(this.selectedBlobFiles);
    let deletedCount = 0;
    let errorCount = 0;

    const deletePromises = filesToDelete.map(filePath => {
      // Extract container name and blob path from full path
      const parts = filePath.split('/');
      const containerName = parts[0] || 'csv-files';
      const blobPath = parts.slice(1).join('/');
      
      return this.transportService.deleteBlobFile(containerName, blobPath).pipe(
        catchError(error => {
          console.error(`Error deleting file ${filePath}:`, error);
          errorCount++;
          return of(null);
        })
      ).toPromise().then(() => {
        deletedCount++;
      });
    });

    Promise.all(deletePromises).then(() => {
      this.isDeletingBlobFiles = false;
      this.selectedBlobFiles.clear();
      
      if (errorCount === 0) {
        this.snackBar.open(`Successfully deleted ${deletedCount} file(s)`, 'OK', {
          duration: 3000
        });
      } else {
        this.snackBar.open(`Deleted ${deletedCount} file(s), ${errorCount} error(s)`, 'OK', {
          duration: 5000,
          panelClass: ['warning-snackbar']
        });
      }
      
      // Reload folders after deletion
      this.loadBlobContainerFolders();
    });
  }

  onClose(): void {
    this.dialogRef.close();
  }
}

















