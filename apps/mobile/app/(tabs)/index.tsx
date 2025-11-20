import { View, Text, StyleSheet, ActivityIndicator } from 'react-native';
import { useState, useEffect } from 'react';
import { getApiUrl, getBaseUrl } from '../../config/api';

export default function HomeScreen() {
  const [apiStatus, setApiStatus] = useState<'loading' | 'connected' | 'error'>('loading');
  const [apiUrl, setApiUrl] = useState<string>('');
  const [errorMessage, setErrorMessage] = useState<string>('');

  useEffect(() => {
    const testApiConnection = async () => {
      const apiUrlWithPrefix = getApiUrl();
      const baseUrl = getBaseUrl();
      setApiUrl(apiUrlWithPrefix);

      try {
        // Health endpoint is at root /health, not /api/health
        const response = await fetch(`${baseUrl}/health`, {
          method: 'GET',
          headers: {
            'Accept': 'application/json',
          },
        });

        if (response.ok) {
          const data = await response.text();
          console.log('✅ API Health Check:', data);
          setApiStatus('connected');
        } else {
          console.error('❌ API Health Check Failed:', response.status);
          setApiStatus('error');
          setErrorMessage(`HTTP ${response.status}`);
        }
      } catch (error) {
        console.error('❌ API Connection Error:', error);
        setApiStatus('error');
        setErrorMessage(error instanceof Error ? error.message : 'Network error');
      }
    };

    testApiConnection();
  }, []);

  return (
    <View style={styles.container}>
      <Text style={styles.title}>🏒 BHM Hockey</Text>
      <Text style={styles.subtitle}>Your hockey events feed</Text>

      <View style={styles.apiTestContainer}>
        <Text style={styles.apiTestTitle}>API Connection Test</Text>
        <Text style={styles.apiUrl}>URL: {apiUrl}</Text>

        {apiStatus === 'loading' && (
          <View style={styles.statusContainer}>
            <ActivityIndicator size="small" color="#003366" />
            <Text style={styles.statusText}>Testing connection...</Text>
          </View>
        )}

        {apiStatus === 'connected' && (
          <View style={styles.statusContainer}>
            <Text style={styles.successText}>✅ Connected to API</Text>
          </View>
        )}

        {apiStatus === 'error' && (
          <View style={styles.statusContainer}>
            <Text style={styles.errorText}>❌ Connection Failed</Text>
            <Text style={styles.errorDetail}>{errorMessage}</Text>
          </View>
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  title: {
    fontSize: 32,
    fontWeight: 'bold',
    marginBottom: 8,
  },
  subtitle: {
    fontSize: 18,
    color: '#666',
    marginBottom: 40,
  },
  apiTestContainer: {
    width: '100%',
    padding: 20,
    backgroundColor: '#f5f5f5',
    borderRadius: 12,
    borderWidth: 1,
    borderColor: '#ddd',
  },
  apiTestTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 8,
    color: '#003366',
  },
  apiUrl: {
    fontSize: 12,
    color: '#666',
    marginBottom: 16,
    fontFamily: 'monospace',
  },
  statusContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  statusText: {
    fontSize: 14,
    color: '#666',
  },
  successText: {
    fontSize: 16,
    color: '#22c55e',
    fontWeight: '600',
  },
  errorText: {
    fontSize: 16,
    color: '#ef4444',
    fontWeight: '600',
  },
  errorDetail: {
    fontSize: 12,
    color: '#ef4444',
    marginTop: 4,
  },
});
