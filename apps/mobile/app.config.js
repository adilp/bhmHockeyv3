export default ({ config }) => {
  // Use EAS_BUILD or CI environment to detect build server
  // This ensures fingerprint matches between local eas update and eas build
  const isEasBuild = process.env.EAS_BUILD === 'true';
  const isLocalDev = !isEasBuild && process.env.NODE_ENV !== 'production';

  return {
    ...config,
    extra: {
      ...config.extra,
      // API URL - always use production for builds/updates, localhost only for local dev server
      apiUrl: process.env.API_URL || 'https://bhmhockey-mb3md.ondigitalocean.app/api',
      // Environment - always production for fingerprint consistency
      environment: 'production',
    }
  };
};
