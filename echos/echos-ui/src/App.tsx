function App() {
  return (
    <div className="app">
      <header className="app__header">
        <h1>ECHOS — Observation</h1>
        <span className="app__badge">U0 · socle</span>
      </header>
      <main>
        <p>
          Interface d'observation et de pilotage de SYNE (React + TypeScript,
          ADR-001 ECHOS).
        </p>
        <p>
          Connexion temps réel (WebSocket :5180) et contrôle (HTTP :5181) :
          jalons U1+.
        </p>
      </main>
    </div>
  )
}

export default App
