import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { AdapterConfigurationService } from './adapter-configuration.service';
import { TransportService } from './transport.service';

describe('AdapterConfigurationService', () => {
  let service: AdapterConfigurationService;
  let transportService: jasmine.SpyObj<TransportService>;

  beforeEach(() => {
    const transportSpy = jasmine.createSpyObj('TransportService', [
      'updateReceiveFolder',
      'updateFileMask',
      'updateBatchSize',
      'updateFieldSeparator',
      'updateCsvPollingInterval',
      'updateSqlConnectionProperties',
      'updateSqlPollingProperties',
      'updateSqlTransactionProperties',
      'updateDestinationReceiveFolder',
      'updateDestinationFileMask',
      'updateDestinationJQScriptFile',
      'updateDestinationSourceAdapterSubscription',
      'updateDestinationSqlStatements',
      'updateSourceAdapterInstance',
      'updateDestinationAdapterInstance',
      'restartAdapter'
    ]);

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, MatSnackBarModule],
      providers: [
        AdapterConfigurationService,
        { provide: TransportService, useValue: transportSpy }
      ]
    });
    service = TestBed.inject(AdapterConfigurationService);
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should update receive folder', (done) => {
    transportService.updateReceiveFolder.and.returnValue(of({}));

    service.updateReceiveFolder('TestInterface', 'csv-files/incoming').subscribe(() => {
      expect(transportService.updateReceiveFolder).toHaveBeenCalledWith('TestInterface', 'csv-files/incoming');
      done();
    });
  });

  it('should update file mask', (done) => {
    transportService.updateFileMask.and.returnValue(of({}));

    service.updateFileMask('TestInterface', '*.csv').subscribe(() => {
      expect(transportService.updateFileMask).toHaveBeenCalledWith('TestInterface', '*.csv');
      done();
    });
  });

  it('should update batch size', (done) => {
    transportService.updateBatchSize.and.returnValue(of({}));

    service.updateBatchSize('TestInterface', 1000).subscribe(() => {
      expect(transportService.updateBatchSize).toHaveBeenCalledWith('TestInterface', 1000);
      done();
    });
  });

  it('should update field separator', (done) => {
    transportService.updateFieldSeparator.and.returnValue(of({}));

    service.updateFieldSeparator('TestInterface', '|').subscribe(() => {
      expect(transportService.updateFieldSeparator).toHaveBeenCalledWith('TestInterface', '|');
      done();
    });
  });

  it('should update CSV polling interval', (done) => {
    transportService.updateCsvPollingInterval.and.returnValue(of({}));

    service.updateCsvPollingInterval('TestInterface', 30).subscribe(() => {
      expect(transportService.updateCsvPollingInterval).toHaveBeenCalledWith('TestInterface', 30);
      done();
    });
  });

  it('should update SQL connection properties', (done) => {
    transportService.updateSqlConnectionProperties.and.returnValue(of({}));

    service.updateSqlConnectionProperties(
      'TestInterface',
      'server',
      'database',
      'user',
      'pass',
      false,
      'rg'
    ).subscribe(() => {
      expect(transportService.updateSqlConnectionProperties).toHaveBeenCalledWith(
        'TestInterface',
        'server',
        'database',
        'user',
        'pass',
        false,
        'rg'
      );
      done();
    });
  });

  it('should update SQL polling properties', (done) => {
    transportService.updateSqlPollingProperties.and.returnValue(of({}));

    service.updateSqlPollingProperties('TestInterface', 'SELECT * FROM Test', 60).subscribe(() => {
      expect(transportService.updateSqlPollingProperties).toHaveBeenCalledWith(
        'TestInterface',
        'SELECT * FROM Test',
        60
      );
      done();
    });
  });

  it('should update SQL transaction properties', (done) => {
    transportService.updateSqlTransactionProperties.and.returnValue(of({}));

    service.updateSqlTransactionProperties('TestInterface', true, 500).subscribe(() => {
      expect(transportService.updateSqlTransactionProperties).toHaveBeenCalledWith(
        'TestInterface',
        true,
        500
      );
      done();
    });
  });

  it('should update destination receive folder', (done) => {
    transportService.updateDestinationReceiveFolder.and.returnValue(of({}));

    service.updateDestinationReceiveFolder('TestInterface', 'csv-files/output').subscribe(() => {
      expect(transportService.updateDestinationReceiveFolder).toHaveBeenCalledWith(
        'TestInterface',
        'csv-files/output'
      );
      done();
    });
  });

  it('should update destination file mask', (done) => {
    transportService.updateDestinationFileMask.and.returnValue(of({}));

    service.updateDestinationFileMask('TestInterface', 'output_*.csv').subscribe(() => {
      expect(transportService.updateDestinationFileMask).toHaveBeenCalledWith(
        'TestInterface',
        'output_*.csv'
      );
      done();
    });
  });

  it('should update destination JQ script file', (done) => {
    transportService.updateDestinationJQScriptFile.and.returnValue(of({}));

    service.updateDestinationJQScriptFile('TestInterface', 'guid-123', 'script.jq').subscribe(() => {
      expect(transportService.updateDestinationJQScriptFile).toHaveBeenCalledWith(
        'TestInterface',
        'guid-123',
        'script.jq'
      );
      done();
    });
  });

  it('should update destination source adapter subscription', (done) => {
    transportService.updateDestinationSourceAdapterSubscription.and.returnValue(of({}));

    service.updateDestinationSourceAdapterSubscription('TestInterface', 'guid-123', 'subscription').subscribe(() => {
      expect(transportService.updateDestinationSourceAdapterSubscription).toHaveBeenCalledWith(
        'TestInterface',
        'guid-123',
        'subscription'
      );
      done();
    });
  });

  it('should update destination SQL statements', (done) => {
    transportService.updateDestinationSqlStatements.and.returnValue(of({}));

    service.updateDestinationSqlStatements(
      'TestInterface',
      'guid-123',
      'INSERT INTO...',
      'UPDATE...',
      'DELETE FROM...'
    ).subscribe(() => {
      expect(transportService.updateDestinationSqlStatements).toHaveBeenCalledWith(
        'TestInterface',
        'guid-123',
        'INSERT INTO...',
        'UPDATE...',
        'DELETE FROM...'
      );
      done();
    });
  });

  it('should update source adapter instance', (done) => {
    transportService.updateSourceAdapterInstance.and.returnValue(of({}));

    service.updateSourceAdapterInstance(
      'TestInterface',
      'guid-123',
      'InstanceName',
      true,
      '{"config": "value"}'
    ).subscribe(() => {
      expect(transportService.updateSourceAdapterInstance).toHaveBeenCalledWith(
        'TestInterface',
        'guid-123',
        'InstanceName',
        true,
        '{"config": "value"}'
      );
      done();
    });
  });

  it('should update destination adapter instance', (done) => {
    transportService.updateDestinationAdapterInstance.and.returnValue(of({}));

    service.updateDestinationAdapterInstance(
      'TestInterface',
      'guid-123',
      'InstanceName',
      true,
      '{"config": "value"}'
    ).subscribe(() => {
      expect(transportService.updateDestinationAdapterInstance).toHaveBeenCalledWith(
        'TestInterface',
        'guid-123',
        'InstanceName',
        true,
        '{"config": "value"}'
      );
      done();
    });
  });

  it('should update source enabled', (done) => {
    transportService.updateSourceAdapterInstance.and.returnValue(of({}));

    service.updateSourceEnabled('TestInterface', 'guid-123', true).subscribe(() => {
      expect(transportService.updateSourceAdapterInstance).toHaveBeenCalledWith(
        'TestInterface',
        'guid-123',
        undefined,
        true,
        undefined
      );
      done();
    });
  });

  it('should update destination enabled', (done) => {
    transportService.updateDestinationAdapterInstance.and.returnValue(of({}));

    service.updateDestinationEnabled('TestInterface', 'guid-123', false).subscribe(() => {
      expect(transportService.updateDestinationAdapterInstance).toHaveBeenCalledWith(
        'TestInterface',
        'guid-123',
        undefined,
        false,
        undefined
      );
      done();
    });
  });

  it('should restart adapter', (done) => {
    transportService.restartAdapter.and.returnValue(of({ message: 'Restarted' }));

    service.restartAdapter('TestInterface', 'Source').subscribe(() => {
      expect(transportService.restartAdapter).toHaveBeenCalledWith('TestInterface', 'Source');
      done();
    });
  });
});

