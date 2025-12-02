import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ServiceBusMessageDialogComponent, ServiceBusMessageDialogData } from './service-bus-message-dialog.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('ServiceBusMessageDialogComponent', () => {
  let component: ServiceBusMessageDialogComponent;
  let fixture: ComponentFixture<ServiceBusMessageDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<ServiceBusMessageDialogComponent>>;

  const mockData: ServiceBusMessageDialogData = {
    message: {
      messageId: 'msg-123',
      interfaceName: 'TestInterface',
      adapterName: 'CSV',
      adapterType: 'Source',
      adapterInstanceGuid: 'guid-123',
      enqueuedTime: new Date(),
      deliveryCount: 1,
      headers: ['Header1', 'Header2'],
      body: { data: 'test' }
    }
  };

  beforeEach(async () => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [
        ServiceBusMessageDialogComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: MatDialogRef, useValue: dialogRefSpy },
        { provide: MAT_DIALOG_DATA, useValue: mockData }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ServiceBusMessageDialogComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<ServiceBusMessageDialogComponent>>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('message display', () => {
    it('should display message ID', () => {
      expect(component.data.message.messageId).toBe('msg-123');
    });

    it('should display interface name', () => {
      expect(component.data.message.interfaceName).toBe('TestInterface');
    });

    it('should display adapter information', () => {
      expect(component.data.message.adapterName).toBe('CSV');
      expect(component.data.message.adapterType).toBe('Source');
    });
  });

  describe('close', () => {
    it('should close dialog', () => {
      component.close();
      expect(dialogRef.close).toHaveBeenCalled();
    });
  });
});
