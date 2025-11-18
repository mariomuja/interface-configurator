import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export type Language = 'de' | 'en' | 'fr' | 'es' | 'it';

export interface Translations {
  [key: string]: {
    de: string;
    en: string;
    fr: string;
    es: string;
    it: string;
  };
}

@Injectable({
  providedIn: 'root'
})
export class TranslationService {
  private currentLanguage$ = new BehaviorSubject<Language>('de');
  
  private translations: Translations = {
    'app.title': {
      de: 'System Interface Configuration',
      en: 'System Interface Configuration',
      fr: 'Configuration d\'interface système',
      es: 'Configuración de interfaz del sistema',
      it: 'Configurazione interfaccia sistema'
    },
    'source.csv': {
      de: 'Quelle - CSV-Daten',
      en: 'Source - CSV Data',
      fr: 'Source - Données CSV',
      es: 'Fuente - Datos CSV',
      it: 'Origine - Dati CSV'
    },
    'destination.sql': {
      de: 'Ziel - SQL Server Tabelle',
      en: 'Destination - SQL Server Table',
      fr: 'Destination - Table SQL Server',
      es: 'Destino - Tabla SQL Server',
      it: 'Destinazione - Tabella SQL Server'
    },
    'records': {
      de: 'Datensätze',
      en: 'Records',
      fr: 'Enregistrements',
      es: 'Registros',
      it: 'Record'
    },
    'transport.start': {
      de: 'Transport starten',
      en: 'Start Transport',
      fr: 'Démarrer le transport',
      es: 'Iniciar transporte',
      it: 'Avvia trasporto'
    },
    'transport.running': {
      de: 'Transport läuft...',
      en: 'Transport running...',
      fr: 'Transport en cours...',
      es: 'Transporte en ejecución...',
      it: 'Trasporto in esecuzione...'
    },
    'table.drop': {
      de: 'Zieltabelle löschen',
      en: 'Delete Destination Table',
      fr: 'Supprimer la table de destination',
      es: 'Eliminar tabla de destino',
      it: 'Elimina tabella di destinazione'
    },
    'table.drop.confirm': {
      de: 'Möchten Sie wirklich die TransportData Tabelle komplett löschen?\n\nDie Tabelle wird beim nächsten CSV-Upload automatisch neu erstellt.',
      en: 'Do you really want to completely delete the TransportData table?\n\nThe table will be automatically recreated on the next CSV upload.',
      fr: 'Voulez-vous vraiment supprimer complètement la table TransportData?\n\nLa table sera automatiquement recréée lors du prochain téléchargement CSV.',
      es: '¿Realmente desea eliminar completamente la tabla TransportData?\n\nLa tabla se recreará automáticamente en la próxima carga CSV.',
      it: 'Vuoi davvero eliminare completamente la tabella TransportData?\n\nLa tabella verrà ricreata automaticamente al prossimo caricamento CSV.'
    },
    'table.empty': {
      de: 'Die Zieltabelle existiert nicht. Beim nächsten CSV-Upload wird die Tabelle mit den CSV-Spalten automatisch neu erstellt.',
      en: 'The destination table has been deleted. On the next CSV upload, the table will be automatically recreated with the CSV columns.',
      fr: 'La table de destination a été supprimée. Lors du prochain téléchargement CSV, la table sera automatiquement recréée avec les colonnes CSV.',
      es: 'La tabla de destino ha sido eliminada. En la próxima carga CSV, la tabla se recreará automáticamente con las columnas CSV.',
      it: 'La tabella di destinazione è stata eliminata. Al prossimo caricamento CSV, la tabella verrà ricreata automaticamente con le colonne CSV.'
    },
    'process.log': {
      de: 'Process Log',
      en: 'Process Log',
      fr: 'Journal des processus',
      es: 'Registro de procesos',
      it: 'Registro processo'
    },
    'log.filter': {
      de: 'Azure-Komponente filtern',
      en: 'Filter Azure Component',
      fr: 'Filtrer le composant Azure',
      es: 'Filtrar componente Azure',
      it: 'Filtra componente Azure'
    },
    'log.filter.all': {
      de: 'Alle Komponenten',
      en: 'All Components',
      fr: 'Tous les composants',
      es: 'Todos los componentes',
      it: 'Tutti i componenti'
    },
    'log.clear': {
      de: 'Protokoll leeren',
      en: 'Clear Log',
      fr: 'Effacer le journal',
      es: 'Limpiar registro',
      it: 'Cancella registro'
    },
    'log.clear.confirm': {
      de: 'Möchten Sie wirklich alle Einträge aus der Protokolltabelle löschen?',
      en: 'Do you really want to delete all entries from the log table?',
      fr: 'Voulez-vous vraiment supprimer toutes les entrées de la table de journal?',
      es: '¿Realmente desea eliminar todas las entradas de la tabla de registro?',
      it: 'Vuoi davvero eliminare tutte le voci dalla tabella di registro?'
    },
    'entries': {
      de: 'Einträge',
      en: 'Entries',
      fr: 'Entrées',
      es: 'Entradas',
      it: 'Voci'
    },
    'no.data.csv': {
      de: 'Keine CSV-Daten verfügbar',
      en: 'No CSV data available',
      fr: 'Aucune donnée CSV disponible',
      es: 'No hay datos CSV disponibles',
      it: 'Nessun dato CSV disponibile'
    },
    'no.data.sql': {
      de: 'Keine SQL-Daten verfügbar',
      en: 'No SQL data available',
      fr: 'Aucune donnée SQL disponible',
      es: 'No hay datos SQL disponibles',
      it: 'Nessun dato SQL disponibile'
    },
    'interface.name': {
      de: 'Schnittstellen-Name',
      en: 'Interface Name',
      fr: 'Nom de l\'interface',
      es: 'Nombre de interfaz',
      it: 'Nome interfaccia'
    },
    'adapter.csv': {
      de: 'CSV',
      en: 'CSV',
      fr: 'CSV',
      es: 'CSV',
      it: 'CSV'
    },
    'adapter.sqlserver': {
      de: 'SQL Server',
      en: 'SQL Server',
      fr: 'SQL Server',
      es: 'SQL Server',
      it: 'SQL Server'
    }
  };

  constructor() {
    // Load saved language from localStorage or default to German
    const savedLanguage = localStorage.getItem('app-language') as Language;
    if (savedLanguage && ['de', 'en', 'fr', 'es', 'it'].includes(savedLanguage)) {
      this.currentLanguage$.next(savedLanguage);
    }
  }

  getCurrentLanguage(): Observable<Language> {
    return this.currentLanguage$.asObservable();
  }

  getCurrentLanguageValue(): Language {
    return this.currentLanguage$.value;
  }

  setLanguage(language: Language): void {
    this.currentLanguage$.next(language);
    localStorage.setItem('app-language', language);
  }

  translate(key: string): string {
    const translation = this.translations[key];
    if (!translation) {
      console.warn(`Translation missing for key: ${key}`);
      return key;
    }
    return translation[this.currentLanguage$.value] || translation['en'] || key;
  }

  getLanguageName(lang: Language): string {
    const names: Record<Language, string> = {
      de: 'Deutsch',
      en: 'English',
      fr: 'Français',
      es: 'Español',
      it: 'Italiano'
    };
    return names[lang];
  }

  getAvailableLanguages(): Language[] {
    return ['de', 'en', 'fr', 'es', 'it'];
  }
}



