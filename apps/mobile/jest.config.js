const path = require('path');

/** @type {import('jest').Config} */
module.exports = {
  // Use ts-jest preset
  preset: 'ts-jest',
  testEnvironment: 'node',

  // Set rootDir to monorepo root so Jest can see all packages
  rootDir: path.resolve(__dirname, '..', '..'),

  // Include both mobile app and packages in roots
  roots: ['<rootDir>/apps/mobile', '<rootDir>/packages'],

  // Only run tests in mobile __tests__ folder
  testMatch: ['<rootDir>/apps/mobile/__tests__/**/*.test.{ts,tsx}'],

  collectCoverageFrom: [
    'apps/mobile/**/*.{ts,tsx}',
    '!**/node_modules/**',
    '!**/.expo/**',
    '!**/coverage/**',
    '!**/*.d.ts',
    '!**/index.ts',
  ],

  moduleFileExtensions: ['ts', 'tsx', 'js', 'jsx', 'json', 'node'],

  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/apps/mobile/$1',
    '^@bhmhockey/shared$': '<rootDir>/packages/shared/src',
    '^@bhmhockey/api-client$': '<rootDir>/packages/api-client/src',
    '^react-native$': '<rootDir>/apps/mobile/__mocks__/react-native.js',
  },

  // Transform ALL TypeScript files including those in packages/
  transform: {
    '^.+\\.(ts|tsx)$': 'ts-jest',
  },

  // Don't ignore @bhmhockey packages in node_modules (if they get resolved there)
  transformIgnorePatterns: [
    '/node_modules/(?!@bhmhockey/)',
  ],

  setupFilesAfterEnv: ['<rootDir>/apps/mobile/jest.setup.js'],
  verbose: true,
};
