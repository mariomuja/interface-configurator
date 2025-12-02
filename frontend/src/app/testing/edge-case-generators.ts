/**
 * Edge Case Data Generators
 */

/**
 * Generate edge case test data
 */
export class EdgeCaseGenerators {
  /**
   * Generate strings with various edge cases
   */
  static generateStringEdgeCases(): string[] {
    return [
      '', // Empty string
      ' ', // Whitespace only
      '  ', // Multiple spaces
      '\t', // Tab
      '\n', // Newline
      '\r\n', // Windows newline
      'a', // Single character
      'a'.repeat(1000), // Very long string
      'a'.repeat(10000), // Extremely long string
      '!@#$%^&*()', // Special characters
      '测试', // Unicode characters
      '🚀🎉', // Emoji
      'null', // String "null"
      'undefined', // String "undefined"
      'true', // String "true"
      'false', // String "false"
      '0', // String "0"
      'NaN', // String "NaN"
      String.fromCharCode(0), // Null character
      '\u200B', // Zero-width space
      '\uFEFF', // Zero-width no-break space
    ];
  }

  /**
   * Generate number edge cases
   */
  static generateNumberEdgeCases(): number[] {
    return [
      0,
      -0,
      1,
      -1,
      Number.MAX_SAFE_INTEGER,
      Number.MIN_SAFE_INTEGER,
      Number.MAX_VALUE,
      Number.MIN_VALUE,
      Number.POSITIVE_INFINITY,
      Number.NEGATIVE_INFINITY,
      Number.NaN,
      Math.PI,
      Math.E,
      0.1 + 0.2, // Floating point precision issue
      1e10,
      1e-10,
    ];
  }

  /**
   * Generate date edge cases
   */
  static generateDateEdgeCases(): Date[] {
    return [
      new Date(0), // Epoch
      new Date('1970-01-01'),
      new Date('2000-01-01'),
      new Date('2038-01-19'), // Year 2038 problem
      new Date('1900-01-01'),
      new Date('9999-12-31'),
      new Date('invalid'), // Invalid date
      new Date(NaN), // Invalid date
      new Date(Date.now()), // Current date
      new Date(Date.now() + 86400000), // Tomorrow
      new Date(Date.now() - 86400000), // Yesterday
    ];
  }

  /**
   * Generate array edge cases
   */
  static generateArrayEdgeCases(): any[][] {
    return [
      [], // Empty array
      [null], // Array with null
      [undefined], // Array with undefined
      [1, 2, 3], // Simple array
      new Array(1000).fill(0), // Large array
      new Array(1000000).fill(0), // Very large array
      [[], [], []], // Nested empty arrays
      [[1, 2], [3, 4]], // Nested arrays
      [{}, {}], // Array of objects
      [1, 'string', null, undefined, {}, []], // Mixed types
    ];
  }

  /**
   * Generate object edge cases
   */
  static generateObjectEdgeCases(): any[] {
    return [
      {}, // Empty object
      { null: null }, // Null value
      { undefined: undefined }, // Undefined value
      { '': 'empty key' }, // Empty key
      { 'key with spaces': 'value' }, // Key with spaces
      { 'key-with-dashes': 'value' }, // Key with dashes
      { 'key.with.dots': 'value' }, // Key with dots
      { '123': 'numeric key' }, // Numeric key
      Object.create(null), // Object without prototype
      { [Symbol('key')]: 'value' }, // Symbol key
      { circular: null as any }, // Will be set to circular reference
    ];
  }

  /**
   * Generate email edge cases
   */
  static generateEmailEdgeCases(): string[] {
    return [
      'test@example.com', // Valid
      'test+tag@example.com', // With plus
      'test.tag@example.com', // With dot
      'test@sub.example.com', // Subdomain
      'test@example.co.uk', // Multiple TLDs
      'test@123.456.789.012', // IP address
      'test@[2001:db8::1]', // IPv6
      '', // Empty
      'invalid', // No @
      '@example.com', // No local part
      'test@', // No domain
      'test..test@example.com', // Double dot
      'test@example..com', // Double dot in domain
      ' test@example.com ', // With spaces
      'test@example.com\n', // With newline
      'test@example.com\0', // With null character
      'a'.repeat(64) + '@example.com', // Very long local part
      'test@' + 'a'.repeat(255) + '.com', // Very long domain
    ];
  }

  /**
   * Generate URL edge cases
   */
  static generateUrlEdgeCases(): string[] {
    return [
      'http://example.com', // HTTP
      'https://example.com', // HTTPS
      'http://example.com/path', // With path
      'http://example.com/path?query=value', // With query
      'http://example.com/path#fragment', // With fragment
      'http://user:pass@example.com', // With auth
      'http://example.com:8080', // With port
      '', // Empty
      'invalid', // Invalid URL
      'javascript:alert(1)', // JavaScript protocol
      'data:text/html,<script>alert(1)</script>', // Data URL
      'http://' + 'a'.repeat(2000) + '.com', // Very long URL
    ];
  }

  /**
   * Generate interface name edge cases
   */
  static generateInterfaceNameEdgeCases(): string[] {
    return [
      'ValidInterface123', // Valid
      'valid-interface', // With dash
      'valid_interface', // With underscore
      '', // Empty
      'ab', // Too short
      'a'.repeat(101), // Too long
      'Interface With Spaces', // With spaces
      'Interface!@#', // Special characters
      '123Interface', // Starts with number
      'interface', // Lowercase only
      'INTERFACE', // Uppercase only
      '测试接口', // Unicode
    ];
  }

  /**
   * Generate all edge cases
   */
  static generateAllEdgeCases(): {
    strings: string[];
    numbers: number[];
    dates: Date[];
    arrays: any[][];
    objects: any[];
    emails: string[];
    urls: string[];
    interfaceNames: string[];
  } {
    return {
      strings: this.generateStringEdgeCases(),
      numbers: this.generateNumberEdgeCases(),
      dates: this.generateDateEdgeCases(),
      arrays: this.generateArrayEdgeCases(),
      objects: this.generateObjectEdgeCases(),
      emails: this.generateEmailEdgeCases(),
      urls: this.generateUrlEdgeCases(),
      interfaceNames: this.generateInterfaceNameEdgeCases()
    };
  }
}
