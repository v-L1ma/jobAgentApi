# Exemplo de Frontend para Job Search Async

## JavaScript Puro (Vanilla JS)

### Função Básica de Polling

```javascript
class JobSearchClient {
  constructor(baseUrl, token) {
    this.baseUrl = baseUrl;
    this.token = token;
    this.pollingInterval = 3000; // 3 segundos
    this.maxPollingAttempts = 30; // Timeout de ~90 segundos
  }

  async searchJobs(query, options = {}) {
    const {
      stack = null,
      location = null,
      page = 1,
      pageSize = 10,
      onProgress = null // Callback para progresso
    } = options;

    let attempts = 0;

    while (attempts < this.maxPollingAttempts) {
      const params = new URLSearchParams();
      if (query) params.append('query', query);
      if (stack) params.append('stack', stack);
      if (location) params.append('location', location);
      params.append('page', page);
      params.append('pageSize', pageSize);

      const response = await fetch(
        `${this.baseUrl}/api/jobs/search?${params.toString()}`,
        {
          headers: {
            'Authorization': `Bearer ${this.token}`,
            'Content-Type': 'application/json'
          }
        }
      );

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const data = await response.json();

      if (onProgress) {
        onProgress(data);
      }

      if (!data.isLoading) {
        // Dados completos disponíveis
        return {
          success: true,
          data: data.data,
          meta: data.meta,
          attempts: attempts + 1
        };
      }

      // Scraping ainda em andamento
      console.log(`Polling attempt ${attempts + 1}/${this.maxPollingAttempts}...`);
      attempts++;

      // Espera antes da próxima tentativa
      await this.sleep(this.pollingInterval);
    }

    // Timeout
    return {
      success: false,
      error: 'Timeout: scraping demorou muito tempo',
      attempts: attempts
    };
  }

  sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
  }
}

// Exemplo de uso
const client = new JobSearchClient('http://localhost:5000', 'seu-token-aqui');

// Busca com callback de progresso
const result = await client.searchJobs('desenvolvedor frontend', {
  onProgress: (data) => {
    if (data.isLoading) {
      console.log('Scraping em andamento...');
      // Atualizar UI com spinner/loading
    } else {
      console.log(`${data.data.length} vagas encontradas!`);
      // Atualizar UI com resultados
    }
  }
});

if (result.success) {
  console.log('Vagas:', result.data);
} else {
  console.error('Erro:', result.error);
}
```

## React

### Hook Personalizado

```typescript
// hooks/useJobSearch.ts
import { useState, useCallback, useRef } from 'react';

interface Job {
  id: string;
  title: string;
  description: string;
  url: string;
  isApplied: boolean;
  company?: string;
  location?: string;
  platform?: string;
}

interface JobSearchMeta {
  scraperRunning: boolean;
  fromCache: boolean;
  totalItems?: number;
  currentPage?: number;
  totalPages?: number;
  requestId?: string;
}

interface UseJobSearchResult {
  jobs: Job[];
  isLoading: boolean;
  isPolling: boolean;
  meta: JobSearchMeta | null;
  error: string | null;
  searchJobs: (query: string) => Promise<void>;
}

export function useJobSearch(): UseJobSearchResult {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isPolling, setIsPolling] = useState(false);
  const [meta, setMeta] = useState<JobSearchMeta | null>(null);
  const [error, setError] = useState<string | null>(null);
  const pollingRef = useRef<boolean>(false);

  const searchJobs = useCallback(async (query: string) => {
    setIsLoading(true);
    setIsPolling(false);
    setError(null);
    pollingRef.current = false;

    const poll = async (attempts: number = 0): Promise<void> => {
      const maxAttempts = 30;
      const pollingInterval = 3000;

      try {
        const response = await fetch(
          `http://localhost:5000/api/jobs/search?query=${encodeURIComponent(query)}`,
          {
            headers: {
              'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
          }
        );

        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }

        const data = await response.json();

        if (!data.isLoading) {
          // Dados completos
          setJobs(data.data);
          setMeta(data.meta);
          setIsLoading(false);
          setIsPolling(false);
          pollingRef.current = false;
          return;
        }

        // Scraping em andamento
        setIsPolling(true);
        pollingRef.current = true;

        if (attempts < maxAttempts) {
          setTimeout(() => poll(attempts + 1), pollingInterval);
        } else {
          throw new Error('Timeout: scraping demorou muito');
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Erro desconhecido');
        setIsLoading(false);
        setIsPolling(false);
        pollingRef.current = false;
      }
    };

    await poll(0);
  }, []);

  return {
    jobs,
    isLoading,
    isPolling,
    meta,
    error,
    searchJobs
  };
}
```

### Componente React

```tsx
// components/JobSearch.tsx
import React, { useState } from 'react';
import { useJobSearch } from '../hooks/useJobSearch';

export const JobSearch: React.FC = () => {
  const [query, setQuery] = useState('');
  const { jobs, isLoading, isPolling, meta, error, searchJobs } = useJobSearch();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (query.trim()) {
      searchJobs(query.trim());
    }
  };

  return (
    <div>
      <form onSubmit={handleSubmit}>
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Buscar vagas..."
        />
        <button type="submit" disabled={isLoading}>
          {isLoading ? 'Buscando...' : 'Buscar'}
        </button>
      </form>

      {error && (
        <div className="error">
          <p>Erro: {error}</p>
        </div>
      )}

      {isPolling && (
        <div className="loading">
          <p>Scraping em andamento, aguarde...</p>
          <div className="spinner" />
        </div>
      )}

      {meta?.fromCache && (
        <div className="cache-notice">
          <p>Dados do cache (atualizado há menos de 15 minutos)</p>
        </div>
      )}

      {jobs.length > 0 && (
        <div className="jobs-list">
          <h2>{meta?.totalItems} vagas encontradas</h2>
          {jobs.map(job => (
            <div key={job.id} className="job-card">
              <h3>{job.title}</h3>
              <p className="company">{job.company} - {job.location}</p>
              <p className="platform">Via {job.platform}</p>
              <p className="description">{job.description.substring(0, 200)}...</p>
              <a href={job.url} target="_blank" rel="noopener noreferrer">
                Ver vaga completa
              </a>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
```

## Vue.js 3

### Composable

```typescript
// composables/useJobSearch.ts
import { ref, Ref } from 'vue';

interface Job {
  id: string;
  title: string;
  description: string;
  url: string;
  isApplied: boolean;
}

export function useJobSearch() {
  const jobs: Ref<Job[]> = ref([]);
  const isLoading = ref(false);
  const isPolling = ref(false);
  const error = ref<string | null>(null);

  const searchJobs = async (query: string) => {
    isLoading.value = true;
    isPolling.value = false;
    error.value = null;

    const poll = async (attempts = 0) => {
      try {
        const response = await fetch(
          `http://localhost:5000/api/jobs/search?query=${encodeURIComponent(query)}`,
          {
            headers: {
              'Authorization': `Bearer ${localStorage.getItem('token')}`
            }
          }
        );

        const data = await response.json();

        if (!data.isLoading) {
          jobs.value = data.data;
          isLoading.value = false;
          isPolling.value = false;
          return;
        }

        isPolling.value = true;

        if (attempts < 30) {
          setTimeout(() => poll(attempts + 1), 3000);
        } else {
          throw new Error('Timeout');
        }
      } catch (err) {
        error.value = err instanceof Error ? err.message : 'Erro';
        isLoading.value = false;
      }
    };

    await poll(0);
  };

  return {
    jobs,
    isLoading,
    isPolling,
    error,
    searchJobs
  };
}
```

## CSS (Estados da UI)

```css
/* Loading State */
.loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 2rem;
}

.spinner {
  width: 40px;
  height: 40px;
  border: 4px solid #f3f3f3;
  border-top: 4px solid #3498db;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

/* Cache Notice */
.cache-notice {
  background: #d4edda;
  color: #155724;
  padding: 0.75rem 1.25rem;
  border-radius: 4px;
  margin: 1rem 0;
}

/* Error State */
.error {
  background: #f8d7da;
  color: #721c24;
  padding: 0.75rem 1.25rem;
  border-radius: 4px;
  margin: 1rem 0;
}

/* Job Card */
.job-card {
  border: 1px solid #ddd;
  border-radius: 8px;
  padding: 1.5rem;
  margin: 1rem 0;
  transition: box-shadow 0.2s;
}

.job-card:hover {
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}

.job-card h3 {
  margin: 0 0 0.5rem 0;
  color: #333;
}

.company, .platform {
  color: #666;
  font-size: 0.9rem;
  margin: 0.25rem 0;
}

.description {
  color: #444;
  line-height: 1.6;
}

.job-card a {
  display: inline-block;
  margin-top: 1rem;
  color: #3498db;
  text-decoration: none;
}

.job-card a:hover {
  text-decoration: underline;
}
```
