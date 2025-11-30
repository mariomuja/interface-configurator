import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { InterfaceManagementService, InterfaceConfiguration } from './interface-management.service';
import { TransportService } from './transport.service';

describe('InterfaceManagementService', () => {
  let service: InterfaceManagementService;
  let transportService: jasmine.SpyObj<TransportService>;

  beforeEach(() => {
    const transportSpy = jasmine.createSpyObj('TransportService', [
      'getInterfaceConfigurations',
      'createInterfaceConfiguration',
      'deleteInterfaceConfiguration',
      'updateInterfaceName'
    ]);

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, MatSnackBarModule],
      providers: [
        InterfaceManagementService,
        { provide: TransportService, useValue: transportSpy }
      ]
    });
    service = TestBed.inject(InterfaceManagementService);
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should return default interface name', () => {
    expect(service.getDefaultInterfaceName()).toBe('FromCsvToSqlServerExample');
  });

  it('should get and set current interface', () => {
    expect(service.getCurrentInterface()).toBe('');
    service.setCurrentInterface('TestInterface');
    expect(service.getCurrentInterface()).toBe('TestInterface');
  });

  it('should emit current interface changes', (done) => {
    let callCount = 0;
    service.currentInterface$.subscribe(name => {
      callCount++;
      if (callCount === 1) {
        // First emission is the initial value
        expect(name).toBe('');
      } else if (callCount === 2) {
        // Second emission is after setCurrentInterface
        expect(name).toBe('TestInterface');
        done();
      }
    });
    service.setCurrentInterface('TestInterface');
  });

  it('should load interfaces', (done) => {
    const mockConfigs: InterfaceConfiguration[] = [
      {
        interfaceName: 'Interface1',
        sourceAdapterName: 'CSV',
        destinationAdapterName: 'SqlServer'
      },
      {
        interfaceName: 'Interface2',
        sourceAdapterName: 'CSV',
        destinationAdapterName: 'SqlServer'
      }
    ];

    transportService.getInterfaceConfigurations.and.returnValue(of(mockConfigs));

    service.loadInterfaces().subscribe(interfaces => {
      expect(interfaces.length).toBe(2);
      expect(interfaces[0].interfaceName).toBe('Interface1');
      expect(service.getInterfaces()).toEqual(interfaces);
      done();
    });
  });

  it('should filter out duplicates and prefer real over placeholder', (done) => {
    const mockConfigs: any[] = [
      { interfaceName: 'Interface1', _isPlaceholder: true },
      { interfaceName: 'Interface1', _isPlaceholder: false },
      { interfaceName: 'Interface2', _isPlaceholder: true }
    ];

    transportService.getInterfaceConfigurations.and.returnValue(of(mockConfigs));

    service.loadInterfaces().subscribe(interfaces => {
      expect(interfaces.length).toBe(2);
      expect(interfaces.find(i => i.interfaceName === 'Interface1')?._isPlaceholder).toBeFalsy();
      expect(interfaces.find(i => i.interfaceName === 'Interface2')?._isPlaceholder).toBeTruthy();
      done();
    });
  });

  it('should get interface by name', () => {
    const mockConfigs: InterfaceConfiguration[] = [
      { interfaceName: 'Interface1' },
      { interfaceName: 'Interface2' }
    ];
    service['interfacesSubject'].next(mockConfigs);
    service.setCurrentInterface('Interface1');

    const interface1 = service.getInterface('Interface1');
    const current = service.getInterface();

    expect(interface1?.interfaceName).toBe('Interface1');
    expect(current?.interfaceName).toBe('Interface1');
  });

  it('should create interface', (done) => {
    const mockCreated: any = {
      interfaceName: 'NewInterface',
      sourceAdapterName: 'CSV',
      destinationAdapterName: 'SqlServer'
    };

    transportService.createInterfaceConfiguration.and.returnValue(of(mockCreated));
    service['interfacesSubject'].next([]);

    service.createInterface('NewInterface').subscribe(created => {
      expect(created.interfaceName).toBe('NewInterface');
      expect(service.getInterfaces().some(i => i.interfaceName === 'NewInterface')).toBeTruthy();
      done();
    });
  });

  it('should delete interface', (done) => {
    const mockConfigs: InterfaceConfiguration[] = [
      { interfaceName: 'Interface1' },
      { interfaceName: 'Interface2' }
    ];
    service['interfacesSubject'].next(mockConfigs);
    service.setCurrentInterface('Interface1');

    transportService.deleteInterfaceConfiguration.and.returnValue(of({}));

    service.deleteInterface('Interface1').subscribe(() => {
      expect(service.getInterfaces().length).toBe(1);
      expect(service.getInterfaces()[0].interfaceName).toBe('Interface2');
      expect(service.getCurrentInterface()).toBe('');
      done();
    });
  });

  it('should update interface name', (done) => {
    const mockConfigs: InterfaceConfiguration[] = [
      { interfaceName: 'OldName' }
    ];
    service['interfacesSubject'].next(mockConfigs);
    service.setCurrentInterface('OldName');

    transportService.updateInterfaceName.and.returnValue(of({}));

    service.updateInterfaceName('OldName', 'NewName').subscribe(() => {
      const updated = service.getInterface('NewName');
      expect(updated?.interfaceName).toBe('NewName');
      expect(service.getCurrentInterface()).toBe('NewName');
      done();
    });
  });

  it('should validate interface with source and destinations', () => {
    const config: InterfaceConfiguration = {
      interfaceName: 'Test',
      sourceAdapterInstanceGuid: 'source-guid',
      destinations: {
        'dest1': { adapterInstanceGuid: 'dest-guid' }
      }
    };
    service['interfacesSubject'].next([config]);

    expect(service.validateInterface('Test')).toBeTruthy();
  });

  it('should invalidate interface with destinations but no source', () => {
    const config: InterfaceConfiguration = {
      interfaceName: 'Test',
      destinations: {
        'dest1': { adapterInstanceGuid: 'dest-guid' }
      }
    };
    service['interfacesSubject'].next([config]);

    expect(service.validateInterface('Test')).toBeFalsy();
  });

  it('should handle errors when loading interfaces', (done) => {
    transportService.getInterfaceConfigurations.and.returnValue(throwError(() => new Error('Test error')));

    service.loadInterfaces().subscribe({
      next: () => fail('Should have thrown error'),
      error: (error) => {
        expect(error).toBeTruthy();
        done();
      }
    });
  });

  it('should ensure default interface exists', (done) => {
    service['interfacesSubject'].next([]);
    const mockCreated: any = {
      interfaceName: 'FromCsvToSqlServerExample',
      sourceAdapterName: 'CSV',
      destinationAdapterName: 'SqlServer'
    };

    transportService.createInterfaceConfiguration.and.returnValue(of(mockCreated));
    transportService.getInterfaceConfigurations.and.returnValue(of([mockCreated]));

    service.ensureDefaultInterface().subscribe(result => {
      expect(result).toBeTruthy();
      done();
    });
  });
});

