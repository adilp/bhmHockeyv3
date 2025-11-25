/** @type {import('jest').Config} */
module.exports = {
  testEnvironment: 'node',
  testMatch: ['**/__tests__/**/*.test.{ts,tsx}'],
  collectCoverageFrom: [
    '**/*.{ts,tsx}',
    '!**/node_modules/**',
    '!**/.expo/**',
    '!**/coverage/**',
    '!**/*.d.ts',
    '!**/index.ts',
  ],
  moduleFileExtensions: ['ts', 'tsx', 'js', 'jsx', 'json', 'node'],
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/$1',
    '^@bhmhockey/shared$': '<rootDir>/../../packages/shared/src',
    '^@bhmhockey/api-client$': '<rootDir>/../../packages/api-client/src',
    '^react-native$': '<rootDir>/__mocks__/react-native.js',
  },
  transform: {
    '^.+\\.(ts|tsx)$': ['babel-jest', { presets: ['@babel/preset-typescript', '@babel/preset-react'] }],
  },
  setupFilesAfterEnv: ['<rootDir>/jest.setup.js'],
  verbose: true,
};
