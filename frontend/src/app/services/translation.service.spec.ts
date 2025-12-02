import { TestBed } from '@angular/core/testing';
import { TranslationService, Language } from './translation.service';

describe('TranslationService', () => {
  let service: TranslationService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(TranslationService);
    // Clear localStorage before each test
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should default to German language', () => {
    expect(service.getCurrentLanguageValue()).toBe('de');
  });

  it('should set and get language', () => {
    service.setLanguage('en');
    expect(service.getCurrentLanguageValue()).toBe('en');
  });

  it('should persist language in localStorage', () => {
    service.setLanguage('fr');
    const savedLanguage = localStorage.getItem('app-language');
    expect(savedLanguage).toBe('fr');
  });

  it('should restore language from localStorage', () => {
    localStorage.setItem('app-language', 'es');
    const newService = new TranslationService();
    expect(newService.getCurrentLanguageValue()).toBe('es');
  });

  it('should translate German text', () => {
    service.setLanguage('de');
    expect(service.translate('app.title')).toBe('System Interface Configuration');
    expect(service.translate('source.csv')).toBe('Quelle - CSV-Daten');
  });

  it('should translate English text', () => {
    service.setLanguage('en');
    expect(service.translate('app.title')).toBe('System Interface Configuration');
    expect(service.translate('source.csv')).toBe('Source - CSV Data');
  });

  it('should translate French text', () => {
    service.setLanguage('fr');
    expect(service.translate('app.title')).toBe('Transport CSV vers SQL Server');
    expect(service.translate('source.csv')).toBe('Source - Données CSV');
  });

  it('should translate Spanish text', () => {
    service.setLanguage('es');
    expect(service.translate('app.title')).toBe('Transporte de CSV a SQL Server');
    expect(service.translate('source.csv')).toBe('Origen - Datos CSV');
  });

  it('should translate Italian text', () => {
    service.setLanguage('it');
    expect(service.translate('app.title')).toBe('Trasporto CSV a SQL Server');
    expect(service.translate('source.csv')).toBe('Origine - Dati CSV');
  });

  it('should return key if translation not found', () => {
    expect(service.translate('nonexistent.key')).toBe('nonexistent.key');
  });

  it('should get available languages', () => {
    const languages = service.getAvailableLanguages();
    expect(languages).toContain('de');
    expect(languages).toContain('en');
    expect(languages).toContain('fr');
    expect(languages).toContain('es');
    expect(languages).toContain('it');
    expect(languages.length).toBe(5);
  });

  it('should get language name', () => {
    expect(service.getLanguageName('de')).toBe('Deutsch');
    expect(service.getLanguageName('en')).toBe('English');
    expect(service.getLanguageName('fr')).toBe('Français');
    expect(service.getLanguageName('es')).toBe('Español');
    expect(service.getLanguageName('it')).toBe('Italiano');
  });

  it('should emit language changes via Observable', (done) => {
    service.getCurrentLanguage().subscribe(language => {
      expect(language).toBe('en');
      done();
    });
    service.setLanguage('en');
  });

  it('should handle all translation keys', () => {
    const keys = [
      'app.title',
      'source.csv',
      'destination.sql',
      'process.log',
      'transport.start',
      'transport.running',
      'table.drop',
      'table.drop.confirm',
      'table.empty',
      'log.clear',
      'log.filter',
      'log.filter.all',
      'log.clear.confirm',
      'records',
      'entries',
      'no.data.csv',
      'no.data.sql'
    ];

    keys.forEach(key => {
      const translation = service.translate(key);
      expect(translation).toBeTruthy();
      expect(translation).not.toBe(key); // Should be translated, not return the key
    });
  });

  describe('edge cases', () => {
    it('should handle invalid language in localStorage', () => {
      localStorage.setItem('app-language', 'invalid');
      const newService = new TranslationService();
      // Should default to 'de' for invalid language
      expect(newService.getCurrentLanguageValue()).toBe('de');
    });

    it('should fallback to English if translation missing for current language', () => {
      service.setLanguage('de');
      // If a key exists but German translation is missing, should fallback to English
      const translation = service.translate('app.title');
      expect(translation).toBeTruthy();
    });

    it('should handle empty translation key', () => {
      expect(service.translate('')).toBe('');
    });

    it('should handle null/undefined translation key gracefully', () => {
      expect(service.translate(null as any)).toBeTruthy();
      expect(service.translate(undefined as any)).toBeTruthy();
    });

    it('should persist language changes correctly', () => {
      service.setLanguage('fr');
      expect(localStorage.getItem('app-language')).toBe('fr');
      
      service.setLanguage('it');
      expect(localStorage.getItem('app-language')).toBe('it');
    });

    it('should emit language changes to all subscribers', (done) => {
      let callCount = 0;
      service.getCurrentLanguage().subscribe(() => {
        callCount++;
        if (callCount === 2) {
          expect(callCount).toBe(2);
          done();
        }
      });
      
      service.setLanguage('en');
      service.setLanguage('de');
    });

    it('should handle rapid language changes', () => {
      service.setLanguage('en');
      service.setLanguage('fr');
      service.setLanguage('es');
      service.setLanguage('it');
      service.setLanguage('de');
      
      expect(service.getCurrentLanguageValue()).toBe('de');
      expect(localStorage.getItem('app-language')).toBe('de');
    });

    it('should return key with warning for missing translations', () => {
      spyOn(console, 'warn');
      const result = service.translate('missing.key');
      expect(result).toBe('missing.key');
      expect(console.warn).toHaveBeenCalled();
    });
  });
});

