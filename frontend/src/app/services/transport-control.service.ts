import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { TransportService } from './transport.service';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable({
  providedIn: 'root'
})
export class TransportControlService {
  constructor(
    private transportService: TransportService,
    private snackBar: MatSnackBar
  ) {}

  startTransport(interfaceName: string, csvContent?: string): Observable<any> {
    return this.transportService.startTransport(interfaceName, csvContent);
  }

  restartAdapter(interfaceName: string, adapterType: 'Source' | 'Destination'): Observable<any> {
    return this.transportService.restartAdapter(interfaceName, adapterType);
  }

  dropTable(): Observable<any> {
    return this.transportService.dropTable();
  }

  clearLogs(): Observable<any> {
    return this.transportService.clearLogs();
  }
}

