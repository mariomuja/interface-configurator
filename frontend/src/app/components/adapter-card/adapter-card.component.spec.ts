import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdapterCardComponent } from './adapter-card.component';
import { provideAnimations } from '@angular/platform-browser/animations';

describe('AdapterCardComponent', () => {
  let component: AdapterCardComponent;
  let fixture: ComponentFixture<AdapterCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdapterCardComponent],
      providers: [provideAnimations()]
    }).compileComponents();

    fixture = TestBed.createComponent(AdapterCardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should emit instanceNameChange on blur', () => {
    spyOn(component.instanceNameChange, 'emit');
    component.instanceName = 'Test Instance';
    component.onInstanceNameBlur();
    expect(component.instanceNameChange.emit).toHaveBeenCalledWith('Test Instance');
  });

  it('should emit instanceNameChange on enter', () => {
    spyOn(component.instanceNameChange, 'emit');
    component.instanceName = 'Test Instance';
    component.onInstanceNameEnter();
    expect(component.instanceNameChange.emit).toHaveBeenCalledWith('Test Instance');
  });

  it('should emit enabledChange when enabled changes', () => {
    spyOn(component.enabledChange, 'emit');
    component.isEnabled = false;
    component.onEnabledChange();
    expect(component.enabledChange.emit).toHaveBeenCalledWith(false);
  });

  it('should emit receiveFolderChange on blur when showReceiveFolder is true', () => {
    spyOn(component.receiveFolderChange, 'emit');
    component.showReceiveFolder = true;
    component.receiveFolder = '/test/folder';
    component.onReceiveFolderBlur();
    expect(component.receiveFolderChange.emit).toHaveBeenCalledWith('/test/folder');
  });

  it('should not emit receiveFolderChange when showReceiveFolder is false', () => {
    spyOn(component.receiveFolderChange, 'emit');
    component.showReceiveFolder = false;
    component.receiveFolder = '/test/folder';
    component.onReceiveFolderBlur();
    expect(component.receiveFolderChange.emit).not.toHaveBeenCalled();
  });

  it('should emit receiveFolderChange on enter when showReceiveFolder is true', () => {
    spyOn(component.receiveFolderChange, 'emit');
    component.showReceiveFolder = true;
    component.receiveFolder = '/test/folder';
    component.onReceiveFolderEnter();
    expect(component.receiveFolderChange.emit).toHaveBeenCalledWith('/test/folder');
  });

  it('should emit restart event', () => {
    spyOn(component.restart, 'emit');
    component.onRestart();
    expect(component.restart.emit).toHaveBeenCalled();
  });

  it('should emit expandedChange event', () => {
    spyOn(component.expandedChange, 'emit');
    component.onExpandedChange(false);
    expect(component.expandedChange.emit).toHaveBeenCalledWith(false);
  });

  it('should emit primaryAction event', () => {
    spyOn(component.primaryAction, 'emit');
    component.onPrimaryAction();
    expect(component.primaryAction.emit).toHaveBeenCalled();
  });

  it('should emit settingsClick event', () => {
    spyOn(component.settingsClick, 'emit');
    component.onSettings();
    expect(component.settingsClick.emit).toHaveBeenCalled();
  });

  describe('getPrimaryActionLabel', () => {
    it('should return "Transport starten" for Source adapter', () => {
      component.adapterType = 'Source';
      expect(component.getPrimaryActionLabel()).toBe('Transport starten');
    });

    it('should return "Tabelle löschen" for Destination adapter', () => {
      component.adapterType = 'Destination';
      expect(component.getPrimaryActionLabel()).toBe('Tabelle löschen');
    });
  });

  describe('getPrimaryActionIcon', () => {
    it('should return "play_arrow" for Source adapter', () => {
      component.adapterType = 'Source';
      expect(component.getPrimaryActionIcon()).toBe('play_arrow');
    });

    it('should return "delete_forever" for Destination adapter', () => {
      component.adapterType = 'Destination';
      expect(component.getPrimaryActionIcon()).toBe('delete_forever');
    });
  });

  describe('getPrimaryActionColor', () => {
    it('should return "primary" for Source adapter', () => {
      component.adapterType = 'Source';
      expect(component.getPrimaryActionColor()).toBe('primary');
    });

    it('should return "accent" for Destination adapter', () => {
      component.adapterType = 'Destination';
      expect(component.getPrimaryActionColor()).toBe('accent');
    });
  });

  it('should have default input values', () => {
    expect(component.adapterType).toBe('Source');
    expect(component.adapterName).toBe('CSV');
    expect(component.instanceName).toBe('');
    expect(component.isEnabled).toBe(true);
    expect(component.receiveFolder).toBe('');
    expect(component.adapterInstanceGuid).toBe('');
    expect(component.isRestarting).toBe(false);
    expect(component.isLoading).toBe(false);
    expect(component.expanded).toBe(true);
    expect(component.showReceiveFolder).toBe(false);
    expect(component.isDisabled).toBe(false);
    expect(component.primaryActionDisabled).toBe(false);
  });
});

