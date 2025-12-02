import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StatisticsDashboardComponent } from './statistics-dashboard.component';
import { TransportService } from '../../services/transport.service';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('StatisticsDashboardComponent', () => {
  let component: StatisticsDashboardComponent;
  let fixture: ComponentFixture<StatisticsDashboardComponent>;
  let transportService: jasmine.SpyObj<TransportService>;

  beforeEach(async () => {
    const transportServiceSpy = jasmine.createSpyObj('TransportService', ['getProcessingStatistics']);

    await TestBed.configureTestingModule({
      imports: [
        StatisticsDashboardComponent,
        NoopAnimationsModule
      ],
      providers: [
        { provide: TransportService, useValue: transportServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StatisticsDashboardComponent);
    component = fixture.componentInstance;
    transportService = TestBed.inject(TransportService) as jasmine.SpyObj<TransportService>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit', () => {
    it('should load statistics on init', () => {
      transportService.getProcessingStatistics.and.returnValue(of({}));
      
      component.ngOnInit();
      
      expect(transportService.getProcessingStatistics).toHaveBeenCalled();
    });

    it('should start auto refresh if enabled', () => {
      component.autoRefresh = true;
      transportService.getProcessingStatistics.and.returnValue(of({}));
      
      component.ngOnInit();
      
      expect(component.refreshSubscription).toBeDefined();
    });
  });

  describe('loadStatistics', () => {
    it('should load statistics successfully', () => {
      const mockStats = { totalRows: 100, succeededRows: 95 };
      transportService.getProcessingStatistics.and.returnValue(of(mockStats));
      
      component.loadStatistics();
      
      expect(component.statistics).toEqual(mockStats);
      expect(component.isLoading).toBe(false);
    });

    it('should handle load error', () => {
      transportService.getProcessingStatistics.and.returnValue(throwError(() => ({ error: { error: 'Load failed' } })));
      
      component.loadStatistics();
      
      expect(component.error).toBeTruthy();
      expect(component.isLoading).toBe(false);
    });
  });

  describe('auto refresh', () => {
    it('should start auto refresh', () => {
      component.autoRefresh = true;
      transportService.getProcessingStatistics.and.returnValue(of({}));
      
      component.startAutoRefresh();
      
      expect(component.refreshSubscription).toBeDefined();
    });

    it('should stop auto refresh', () => {
      component.startAutoRefresh();
      component.stopAutoRefresh();
      
      expect(component.refreshSubscription?.closed).toBeTruthy();
    });
  });

  describe('ngOnDestroy', () => {
    it('should unsubscribe from refresh subscription', () => {
      component.startAutoRefresh();
      const subscription = component.refreshSubscription;
      
      component.ngOnDestroy();
      
      expect(subscription?.closed).toBeTruthy();
    });
  });
});
