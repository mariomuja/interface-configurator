/**
 * Test Fixtures - Pre-configured test scenarios
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { TransportService } from '../services/transport.service';
import { AuthService } from '../services/auth.service';
import { CsvRecordBuilder, SqlRecordBuilder, ProcessLogBuilder, InterfaceConfigurationBuilder } from './test-data-builders';

/**
 * Common test fixture configuration
 */
export class TestFixtures {
  /**
   * Create a basic component test fixture
   */
  static async createComponentFixture<T>(
    componentClass: any,
    providers: any[] = [],
    imports: any[] = []
  ): Promise<ComponentFixture<T>> {
    await TestBed.configureTestingModule({
      imports: [
        componentClass,
        HttpClientTestingModule,
        NoopAnimationsModule,
        MatDialogModule,
        MatSnackBarModule,
        ...imports
      ],
      providers: [...providers]
    }).compileComponents();

    return TestBed.createComponent<T>(componentClass);
  }

  /**
   * Create a fixture with TransportService mock
   */
  static createTransportServiceMock(): jasmine.SpyObj<TransportService> {
    return jasmine.createSpyObj('TransportService', [
      'getBlobContainerFolders',
      'deleteBlobFile',
      'getSampleCsvData',
      'getSqlData',
      'getProcessLogs',
      'startTransport',
      'clearTable',
      'dropTable',
      'clearLogs',
      'getInterfaceConfigurations',
      'createInterfaceConfiguration',
      'getInterfaceConfiguration',
      'deleteInterfaceConfiguration',
      'getDestinationAdapterInstances',
      'addDestinationAdapterInstance',
      'removeDestinationAdapterInstance'
    ]);
  }

  /**
   * Create a fixture with AuthService mock
   */
  static createAuthServiceMock(): jasmine.SpyObj<AuthService> {
    return jasmine.createSpyObj('AuthService', [
      'login',
      'logout',
      'isAuthenticated',
      'getCurrentUser'
    ]);
  }

  /**
   * Create a complete transport component scenario
   */
  static createTransportScenario() {
    const transportService = this.createTransportServiceMock();
    
    // Setup default mock responses
    transportService.getSqlData.and.returnValue(
      of(SqlRecordBuilder.create().withName('Test').buildArray(5))
    );
    transportService.getProcessLogs.and.returnValue(
      of(ProcessLogBuilder.create().asInfo().buildArray(10))
    );
    transportService.getInterfaceConfigurations.and.returnValue(
      of([InterfaceConfigurationBuilder.create().build()])
    );
    transportService.getBlobContainerFolders.and.returnValue(of([]));

    return {
      transportService,
      mockData: {
        sqlRecords: SqlRecordBuilder.create().buildArray(5),
        processLogs: ProcessLogBuilder.create().buildArray(10),
        interfaceConfigs: [InterfaceConfigurationBuilder.create().build()]
      }
    };
  }

  /**
   * Create a complete authentication scenario
   */
  static createAuthScenario() {
    const authService = this.createAuthServiceMock();
    
    authService.login.and.returnValue(
      of({
        success: true,
        token: 'mock-token',
        user: { id: 1, username: 'testuser', role: 'user' }
      })
    );
    authService.isAuthenticated.and.returnValue(true);
    authService.getCurrentUser.and.returnValue({
      id: 1,
      username: 'testuser',
      role: 'user'
    });

    return {
      authService,
      mockUser: {
        id: 1,
        username: 'testuser',
        role: 'user'
      }
    };
  }

  /**
   * Create a dialog test scenario
   */
  static createDialogScenario() {
    const dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);
    const snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);

    return {
      dialogRef,
      snackBar
    };
  }

  /**
   * Create a form validation scenario
   */
  static createFormValidationScenario() {
    return {
      validData: {
        interfaceName: 'ValidInterfaceName123',
        email: 'test@example.com',
        batchSize: 100
      },
      invalidData: {
        interfaceName: 'ab', // Too short
        email: 'invalid-email',
        batchSize: -1
      },
      edgeCases: {
        emptyString: '',
        veryLongString: 'a'.repeat(1000),
        specialCharacters: '!@#$%^&*()',
        unicode: '测试接口名称',
        numbers: '1234567890'
      }
    };
  }

  /**
   * Create an error scenario
   */
  static createErrorScenario() {
    return {
      networkError: {
        status: 0,
        message: 'Network error'
      },
      serverError: {
        status: 500,
        message: 'Internal server error'
      },
      notFoundError: {
        status: 404,
        message: 'Not found'
      },
      unauthorizedError: {
        status: 401,
        message: 'Unauthorized'
      },
      forbiddenError: {
        status: 403,
        message: 'Forbidden'
      }
    };
  }
}

// Import of for RxJS
import { of } from 'rxjs';
