export default ({ config }) => {
  const isDev = process.env.NODE_ENV !== 'production';

  return {
    ...config,
    extra: {
      ...config.extra,
      // API URL configuration
      apiUrl: process.env.API_URL || (isDev
        ? 'http://localhost:5001/api'
        : 'https://your-app.ondigitalocean.app/api'),
      // Environment
      environment: isDev ? 'development' : 'production',
    }
  };
};
