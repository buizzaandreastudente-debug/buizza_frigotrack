&lt;div align="center"&gt;

# ❄️ FrigoTrack

**Un piccolo sistema per controllare la temperatura di celle frigorifere, camion e magazzini.**

Sensori → API sicure → allarmi → salvataggio dati

&lt;/div&gt;

---

## 🧊 Cos'è FrigoTrack?

**FrigoTrack** è un progetto dimostrativo che simula dei sensori di temperatura collegati a un server.

I sensori inviano le misure al server, il server controlla che la temperatura sia corretta e, se qualcosa non va, genera un **allarme**.

Per esempio:

- i farmaci devono stare tra **2°C e 8°C**;
- il cibo fresco tra **0°C e 5°C**;
- i prodotti surgelati tra **-30°C e -18°C**.

Se la temperatura esce da questi intervalli, FrigoTrack segnala subito il problema.

---

## ✨ Cosa fa

- 📡 Simula più sensori contemporaneamente.
- 🔐 Crea una sessione sicura con **Diffie-Hellman**.
- 🛡️ Cifra i messaggi con **AES-GCM**.
- ✅ Controlla che i messaggi JSON siano validi.
- 🌡️ Riceve temperatura, umidità e batteria.
- 🚨 Genera allarmi se la temperatura è troppo alta o troppo bassa.
- 💾 Salva le misure in un file JSON.
- 🔎 Permette di recuperare le misure di un sensore.
- 🧪 Include test automatici con **xUnit**.

---

## 🚀 Avvio rapido

### 1. Requisiti

Serve installato:

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

### 2. Apri il progetto

```bash
cd FrigoTrack