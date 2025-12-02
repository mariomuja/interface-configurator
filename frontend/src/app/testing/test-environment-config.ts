/**
 * Test Environment Configuration
 */

/**
 * Test environment types
 */
export type TestEnvironment = 'unit' | 'integration' | 'e2e' | 'ci' | 'local';

/**
 * Test environment configuration
 */
export interface TestEnvironmentConfig {
  environment: TestEnvironment;
  baseUrl: string;
  apiUrl: string;
  timeout: number;
  retries: number;
  parallel: boolean;
  headless: boolean;
  debug: boolean;
  coverage: boolean;
  mockApi: boolean;
}

/**
 * Default configurations for each environment
 */
const environmentConfigs: Record<TestEnvironment, Partial<TestEnvironmentConfig>> = {
  unit: {
    baseUrl: 'http://localhost:9876',
    apiUrl: 'http://localhost:7071/api',
    timeout: 5000,
    retries: 0,
    parallel: true,
    headless: true,
    debug: false,
    coverage: true,
    mockApi: true
  },
  integration: {
    baseUrl: 'http://localhost:4200',
    apiUrl: 'http://localhost:7071/api',
    timeout: 10000,
    retries: 1,
    parallel: false,
    headless: true,
    debug: false,
    coverage: true,
    mockApi: false
  },
  e2e: {
    baseUrl: 'http://localhost:4200',
    apiUrl: 'https://func-integration-main.azurewebsites.net/api',
    timeout: 30000,
    retries: 2,
    parallel: true,
    headless: true,
    debug: false,
    coverage: false,
    mockApi: false
  },
  ci: {
    baseUrl: process.env.CI_BASE_URL || 'http://localhost:4200',
    apiUrl: process.env.CI_API_URL || 'https://func-integration-main.azurewebsites.net/api',
    timeout: 60000,
    retries: 3,
    parallel: true,
    headless: true,
    debug: false,
    coverage: true,
    mockApi: false
  },
  local: {
    baseUrl: 'http://localhost:4200',
    apiUrl: 'http://localhost:7071/api',
    timeout: 30000,
    retries: 0,
    parallel: false,
    headless: false,
    debug: true,
    coverage: false,
    mockApi: false
  }
};

/**
 * Test environment manager
 */
export class TestEnvironmentManager {
  private static currentEnvironment: TestEnvironment = this.detectEnvironment();
  private static config: TestEnvironmentConfig = this.getConfig(this.currentEnvironment);

  /**
   * Detect current test environment
   */
  private static detectEnvironment(): TestEnvironment {
    if (process.env.CI === 'true' || process.env.CI === '1') {
      return 'ci';
    }
    if (process.env.TEST_ENV) {
      return process.env.TEST_ENV as TestEnvironment;
    }
    return 'unit';
  }

  /**
   * Get configuration for environment
   */
  static getConfig(environment: TestEnvironment = this.currentEnvironment): TestEnvironmentConfig {
    const baseConfig = environmentConfigs[environment];
    return {
      environment,
      baseUrl: baseConfig.baseUrl || 'http://localhost:4200',
      apiUrl: baseConfig.apiUrl || 'http://localhost:7071/api',
      timeout: baseConfig.timeout || 5000,
      retries: baseConfig.retries || 0,
      parallel: baseConfig.parallel ?? true,
      headless: baseConfig.headless ?? true,
      debug: baseConfig.debug ?? false,
      coverage: baseConfig.coverage ?? false,
      mockApi: baseConfig.mockApi ?? false
    };
  }

  /**
   * Get current environment
   */
  static getCurrentEnvironment(): TestEnvironment {
    return this.currentEnvironment;
  }

  /**
   * Get current configuration
   */
  static getCurrentConfig(): TestEnvironmentConfig {
    return this.config;
  }

  /**
   * Set environment
   */
  static setEnvironment(environment: TestEnvironment): void {
    this.currentEnvironment = environment;
    this.config = this.getConfig(environment);
  }

  /**
   * Override configuration
   */
  static overrideConfig(overrides: Partial<TestEnvironmentConfig>): void {
    this.config = { ...this.config, ...overrides };
  }

  /**
   * Is CI environment
   */
  static isCI(): boolean {
    return this.currentEnvironment === 'ci';
  }

  /**
   * Is local environment
   */
  static isLocal(): boolean {
    return this.currentEnvironment === 'local';
  }

  /**
   * Should mock API
   */
  static shouldMockApi(): boolean {
    return this.config.mockApi;
  }

  /**
   * Get API URL
   */
  static getApiUrl(): string {
    return this.config.apiUrl;
  }

  /**
   * Get base URL
   */
  static getBaseUrl(): string {
    return this.config.baseUrl;
  }
}
