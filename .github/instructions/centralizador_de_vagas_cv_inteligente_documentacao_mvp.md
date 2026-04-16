# 💼 Centralizador de Vagas + CV Inteligente

## 🎯 Visão Geral

Este sistema tem como objetivo centralizar vagas relevantes para o usuário e aumentar suas chances de contratação através da geração automática de currículos personalizados para cada vaga.

---

## 💡 Proposta de Valor

> Encontre vagas que fazem sentido para você e gere currículos otimizados automaticamente para aumentar suas chances de ser chamado para entrevistas.

---

## 🧠 Problemas Resolvidos

- Dificuldade em encontrar vagas relevantes
- Baixa taxa de resposta em candidaturas
- Currículos genéricos e pouco otimizados
- Falta de tempo para adaptar currículo para cada vaga

---

## 🚀 Funcionalidades do MVP

### 1. Cadastro do Usuário

- Upload do currículo (PDF)
- Informações adicionais:
  - Stack tecnológica
  - Nível (Júnior, Pleno, Sênior)
  - Preferências (remoto, salário, localização)

---

### 2. Integração com Crawler (já existente)

O sistema utilizará um crawler já desenvolvido para buscar vagas.

#### Responsabilidades do crawler:
- Coletar vagas de diferentes plataformas
- Retornar dados estruturados

#### Estrutura esperada da vaga:

```json
{
  "title": "Frontend Developer",
  "company": "Empresa X",
  "description": "Descrição da vaga",
  "location": "Remoto",
  "url": "link-da-vaga"
}
```

---

### 3. Análise de Vaga (IA)

Para cada vaga coletada:

#### Entrada:
- Descrição da vaga

#### Saída:
```json
{
  "skillsRequired": ["React", "TypeScript"],
  "level": "Junior",
  "keywords": ["frontend", "componentes"]
}
```

---

### 4. Match Inteligente

Comparação entre:
- Currículo do usuário
- Requisitos da vaga

#### Resultado:
- Percentual de compatibilidade
- Lista de skills compatíveis
- Lista de gaps (o que falta)

---

### 5. Geração de Currículo Personalizado (CORE)

#### Ação:
Botão: **"Adaptar currículo para esta vaga"**

#### Entrada:
- Currículo original
- Dados da vaga

#### Saída:
- Novo currículo otimizado
- Ajuste de palavras-chave
- Destaque de experiências relevantes

---

### 6. (Opcional) Geração de Cover Letter

- Baseada na vaga
- Personalizada para a empresa

---

### 7. Dashboard do Usuário

- Lista de vagas recomendadas
- Status das candidaturas (manual)
- Histórico de currículos gerados

---

## 🧱 Arquitetura

### Backend
- ASP.NET Core (.NET 8)

### Frontend
- Next.js (React)

### Integrações
- Crawler (interno)
- API de IA (LLM)

### Armazenamento
- AWS S3 (currículos)
- Banco de dados (usuários e vagas)

---

## 📂 Estrutura do Projeto

```
src/
 ├── Api/
 ├── Application/
 ├── Domain/
 ├── Infrastructure/
```

---

## 🔌 Endpoints

### POST /api/user/upload-cv

Upload do currículo do usuário

---

### GET /api/jobs

Retorna vagas coletadas pelo crawler

---

### POST /api/jobs/analyze

Analisa uma vaga

---

### POST /api/match

Calcula compatibilidade entre usuário e vaga

---

### POST /api/cv/generate

Gera currículo personalizado

#### Request:
```json
{
  "cv": "texto do currículo",
  "jobDescription": "texto da vaga"
}
```

#### Response:
```json
{
  "generatedCv": "currículo otimizado"
}
```

---

## 🤖 Prompt de IA

### Análise de vaga

```
Analise a vaga abaixo e extraia:
- habilidades exigidas
- nível de senioridade
- palavras-chave

Vaga:
{{job_description}}
```

---

### Geração de currículo

```
Adapte o currículo abaixo para a vaga.

Foque em:
- palavras-chave da vaga
- experiências relevantes
- clareza e objetividade

Currículo:
{{cv}}

Vaga:
{{job}}
```

---

## 🔄 Fluxo do Usuário

1. Usuário sobe currículo
2. Sistema carrega vagas via crawler
3. Usuário visualiza vagas recomendadas
4. Seleciona uma vaga
5. Visualiza match
6. Clica em "Adaptar currículo"
7. Baixa o novo currículo
8. Aplica manualmente

---

## 💰 Monetização

### Plano Gratuito
- Até 3 currículos gerados por mês

### Plano Pago
- Currículos ilimitados
- Melhor qualidade de IA
- Histórico completo

---

## ⚠️ Considerações Técnicas

- Evitar scraping agressivo
- Cachear vagas do crawler
- Controlar custo de IA
- Sanitizar entradas
- Limitar tamanho de arquivos

---

## 📈 Evoluções Futuras

- Extensão de navegador
- Sugestões de melhoria de perfil
- Simulação de entrevista
- Ranking de compatibilidade

---

## 🧠 Insight Final

O valor do sistema não está apenas em encontrar vagas, mas em aumentar significativamente a chance do usuário ser chamado para entrevistas.

---

## ✅ Definição de Sucesso

- Aumento na taxa de resposta das candidaturas
- Usuários gerando múltiplos currículos
- Percepção clara de valor pelo usuário

---

**Fim da documentação MVP** 🚀

