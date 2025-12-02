/**
 * Snapshot Testing Helpers
 */

import { ComponentFixture } from '@angular/core/testing';
import { DebugElement } from '@angular/core';

/**
 * Snapshot testing utilities
 */
export class SnapshotHelpers {
  /**
   * Create a snapshot of component HTML
   */
  static createHtmlSnapshot(fixture: ComponentFixture<any>): string {
    return fixture.nativeElement.innerHTML;
  }

  /**
   * Create a snapshot of component state
   */
  static createStateSnapshot(component: any): Record<string, any> {
    const snapshot: Record<string, any> = {};
    Object.keys(component).forEach(key => {
      if (!key.startsWith('_') && typeof component[key] !== 'function') {
        try {
          snapshot[key] = JSON.parse(JSON.stringify(component[key]));
        } catch (e) {
          snapshot[key] = String(component[key]);
        }
      }
    });
    return snapshot;
  }

  /**
   * Compare HTML snapshots
   */
  static compareHtmlSnapshots(
    current: string,
    expected: string,
    ignoreAttributes: string[] = []
  ): { match: boolean; differences: string[] } {
    const differences: string[] = [];
    
    if (current !== expected) {
      differences.push('HTML structure differs');
      
      // Simple diff (for basic comparison)
      const currentLines = current.split('\n');
      const expectedLines = expected.split('\n');
      
      const maxLines = Math.max(currentLines.length, expectedLines.length);
      for (let i = 0; i < maxLines; i++) {
        if (currentLines[i] !== expectedLines[i]) {
          differences.push(`Line ${i + 1}: Expected "${expectedLines[i]}", got "${currentLines[i]}"`);
        }
      }
    }
    
    return {
      match: differences.length === 0,
      differences
    };
  }

  /**
   * Compare state snapshots
   */
  static compareStateSnapshots(
    current: Record<string, any>,
    expected: Record<string, any>
  ): { match: boolean; differences: string[] } {
    const differences: string[] = [];
    const allKeys = new Set([...Object.keys(current), ...Object.keys(expected)]);
    
    allKeys.forEach(key => {
      if (!(key in current)) {
        differences.push(`Missing key: ${key}`);
      } else if (!(key in expected)) {
        differences.push(`Unexpected key: ${key}`);
      } else {
        const currentValue = JSON.stringify(current[key]);
        const expectedValue = JSON.stringify(expected[key]);
        if (currentValue !== expectedValue) {
          differences.push(`Key "${key}": Expected ${expectedValue}, got ${currentValue}`);
        }
      }
    });
    
    return {
      match: differences.length === 0,
      differences
    };
  }

  /**
   * Create component structure snapshot
   */
  static createStructureSnapshot(fixture: ComponentFixture<any>): any {
    const element = fixture.nativeElement;
    return {
      tagName: element.tagName,
      className: element.className,
      id: element.id,
      attributes: Array.from(element.attributes).map((attr: any) => ({
        name: attr.name,
        value: attr.value
      })),
      childCount: element.children.length,
      textContent: element.textContent?.trim().substring(0, 100) // First 100 chars
    };
  }

  /**
   * Normalize HTML for comparison (remove whitespace, normalize attributes)
   */
  static normalizeHtml(html: string): string {
    return html
      .replace(/\s+/g, ' ')
      .replace(/>\s+</g, '><')
      .trim();
  }
}
