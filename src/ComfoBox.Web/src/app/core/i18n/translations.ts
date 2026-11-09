/**
 * Die Übersetzungen der Oberfläche.
 *
 * <p>Die Schlüssel sind typisiert: fehlt in einer Sprache ein Eintrag, bricht
 * der TypeScript-Build ab. Das ist Absicht - so kann keine Sprache
 * „halbfertig" ausgeliefert werden.</p>
 *
 * <p>Werte mit Platzhaltern sind Funktionen. Sie werden im Template aufgerufen
 * (`{{ t().x.y(1, 2) }}`) und liefern den fertigen Text.</p>
 */

/** Die unterstützten Sprachen. */
export type Language = 'de' | 'en' | 'fr';

/** Eine Auswahlmöglichkeit für die Sprachwahl. */
export interface LanguageOption {
  /** Der Sprachcode. */
  readonly code: Language;
  /** Der Name der Sprache in der Sprache selbst. */
  readonly label: string;
}

/** Die Texte der Oberfläche. */
export interface Translations {
  readonly language: {
    readonly label: string;
    readonly de: string;
    readonly en: string;
    readonly fr: string;
  };

  readonly common: {
    readonly save: string;
    readonly cancel: string;
    readonly reload: string;
    readonly back: string;
    readonly next: string;
    readonly pause: string;
    readonly dash: string;
    readonly duration: (milliseconds: number) => string;
  };

  readonly nav: {
    readonly dashboard: string;
    readonly items: string;
    readonly config: string;
    readonly diagnostics: string;
    readonly help: string;
    readonly logout: string;
    readonly toggleMenu: string;
    readonly mainNavigation: string;
  };

  readonly login: {
    readonly title: string;
    readonly hint: string;
    readonly username: string;
    readonly password: string;
    readonly submit: string;
    readonly helpLink: string;
    readonly invalid: string;
  };

  readonly dashboard: {
    readonly title: string;
    readonly simulationTitle: string;
    readonly simulationHeading: string;
    readonly simulationText: string;
    readonly bacnetDownTitle: string;
    readonly bacnetDownHeading: string;
    readonly bacnetDownFallback: string;
    readonly bacnetDownPort: (port: string) => string;
    readonly bacnet: string;
    readonly mqttBroker: string;
    readonly gateway: string;
    readonly writes: string;
    readonly writesRemaining: (remaining: number) => string;
    readonly writesNoLimit: string;
    readonly writesNearLimit: string;
    readonly status: string;
    readonly connected: string;
    readonly disconnected: string;
    readonly simulation: string;
    readonly version: string;
    readonly device: (id: string) => string;
    readonly uptime: (value: string) => string;
    readonly operation: string;
    readonly operationMode: string;
    readonly setPoint: string;
    readonly heatPump: string;
    readonly roomTemperature: string;
    readonly outdoorTemperature: string;
    readonly warmWater: string;
    readonly range: (min: string, max: string, unit: string) => string;
    readonly openInItems: string;
    readonly emptyCatalog: string;
    readonly quickActions: string;
    readonly restart: string;
    readonly reconnectBacnet: string;
    readonly reconnectMqtt: string;
    readonly openConfig: string;
    readonly rerunSetup: string;
    readonly restartTitle: string;
    readonly restartText: string;
    readonly restartAccept: string;
    readonly restartInfo: string;
    readonly reconnectBacnetInfo: string;
    readonly reconnectMqttInfo: string;
  };

  readonly items: {
    readonly title: string;
    readonly search: string;
    readonly searchAria: string;
    readonly onlyWritable: string;
    readonly onlyExpert: string;
    readonly onlyStale: string;
    readonly count: (visible: number, total: number) => string;
    readonly reload: string;
    readonly reset: string;
    readonly loading: string;
    readonly empty: string;
  };

  readonly item: {
    readonly stale: string;
    readonly expert: string;
    readonly save: string;
    readonly choose: string;
    readonly readonly: string;
    readonly newValue: (name: string) => string;
    readonly topic: (topic: string) => string;
  };

  readonly config: {
    readonly title: string;
    readonly general: string;
    readonly baseTopic: string;
    readonly baseTopicHint: string;
    readonly pollInterval: string;
    readonly pollIntervalHint: string;
    readonly retain: string;
    readonly retainHint: string;
    readonly writeProtection: string;
    readonly maxWrites: string;
    readonly maxWritesHint: string;
    readonly expertMode: string;
    readonly expertModeHint: string;
    readonly expertModeConfirmTitle: string;
    readonly expertModeConfirmText: string;
    readonly expertModeConfirmAccept: string;
    readonly bacnetSection: string;
    readonly appliesImmediately: string;
    readonly deviceId: string;
    readonly mstpAddress: string;
    readonly itemTimeout: string;
    readonly mqttSection: string;
    readonly restartRequired: string;
    readonly discovery: string;
    readonly discoveryPrefix: string;
    readonly active: string;
    readonly inactive: string;
    readonly broker: string;
    readonly port: string;
    readonly user: string;
    readonly password: string;
    readonly set: string;
    readonly tls: string;
    readonly noBroker: string;
    readonly tokens: string;
    readonly tokensHint: string;
    readonly tokenName: string;
    readonly tokenScope: string;
    readonly tokenScopeRead: string;
    readonly tokenScopeControl: string;
    readonly tokenScopeAdmin: string;
    readonly tokenExpiry: string;
    readonly tokenExpiryNever: string;
    readonly tokenExpiryDays: (days: number) => string;
    readonly tokenCreate: string;
    readonly tokenCreated: string;
    readonly tokenCreatedHint: string;
    readonly tokenNameColumn: string;
    readonly tokenId: string;
    readonly tokenCreatedAt: string;
    readonly tokenScopeColumn: string;
    readonly tokenExpires: string;
    readonly tokenExpired: string;
    readonly tokenNever: string;
    readonly tokenRevoke: (name: string) => string;
    readonly tokenRevokeTitle: string;
    readonly tokenRevokeText: (name: string) => string;
    readonly tokenRevokeAccept: string;
    readonly tokenRevokeDone: string;
    readonly tokenNone: string;
    readonly security: string;
    readonly securityHint: string;
    readonly requireAuth: string;
    readonly requireAuthHint: string;
    readonly adminUser: string;
    readonly currentPassword: string;
    readonly newPassword: string;
    readonly repeatPassword: string;
    readonly changeAdmin: string;
    readonly passwordMismatch: string;
    readonly passwordChanged: string;
    readonly settingsSaved: string;
    readonly saved: string;
    readonly savedHint: string;
    readonly diagnostics: string;
    readonly restart: string;
    readonly restartTitle: string;
    readonly restartText: string;
    readonly restartAccept: string;
    readonly restartInfo: string;
  };

  readonly help: {
    readonly title: string;
    readonly lead: string;
    readonly search: string;
    readonly searchAria: string;
    readonly empty: string;
    readonly allPages: string;
    readonly guide: string;
    readonly reference: string;
    readonly unavailable: string;
  };

  readonly health: {
    readonly unreachable: string;
    readonly unreachableSince: (value: string) => string;
    readonly unreachableMoment: string;
    readonly troubleshooting: string;
    readonly degraded: string;
    readonly error: string;
  };

  readonly setup: {
    readonly title: string;
    readonly progress: (step: number, total: number, percent: number) => string;
    readonly steps: string;
    readonly back: string;
    readonly next: string;
  };

  readonly diagnostics: {
    readonly title: string;
    readonly tabChecks: string;
    readonly tabErrors: string;
    readonly tabLogs: string;
    readonly tabWrites: string;
    readonly tabConnections: string;
    readonly tabBus: string;
    readonly component: string;
    readonly source: string;
    readonly state: string;
    readonly message: string;
    readonly since: string;
    readonly noChecks: string;
    readonly severity: string;
    readonly severityAria: string;
    readonly all: string;
    readonly noErrors: string;
    readonly loading: string;
    readonly writeCounter: string;
    readonly resetsAt: (value: string) => string;
    readonly uptime: string;
    readonly startedAt: (value: string) => string;
    readonly connected: string;
    readonly disconnected: string;
    readonly portLabel: (value: string) => string;
    readonly brokerLabel: (value: string) => string;
    readonly deviceLabel: (value: string | number) => string;
    readonly busReadOnly: string;
    readonly busTokenNote: string;
    readonly durationLabel: string;
    readonly durationAria: string;
    readonly pausePolling: string;
    readonly startObservation: string;
    readonly stopObservation: string;
    readonly loadingBus: string;
    readonly busCard: string;
    readonly eventsIn: (seconds: string) => string;
    readonly interfaceCard: string;
    readonly interfaceHint: (baudrate: number, station: number) => string;
    readonly stationsSeen: string;
    readonly pollingPaused: string;
    readonly pollingRunning: string;
    readonly time: string;
    readonly event: string;
    readonly station: string;
    readonly frame: string;
    readonly noEvents: string;
    readonly kindTokenReceived: string;
    readonly kindTokenUsed: string;
    readonly kindTokenPassed: string;
    readonly kindPollForMaster: string;
    readonly kindSoleMaster: string;
    readonly kindFrameReceived: string;
    readonly kindFrameSent: string;
    readonly kindDataReceived: string;
    readonly kindReplyPosted: string;
  };

  readonly logs: {
    readonly level: string;
    readonly levelAria: string;
    readonly allSources: string;
    readonly sourceAria: string;
    readonly search: string;
    readonly searchAria: string;
    readonly rangeAria: string;
    readonly rangeAll: string;
    readonly range5m: string;
    readonly range15m: string;
    readonly range1h: string;
    readonly live: string;
    readonly autoScroll: string;
    readonly entries: (count: number) => string;
    readonly reload: string;
    readonly reloadAria: string;
    readonly reset: string;
    readonly export: string;
    readonly clear: string;
    readonly reconnecting: string;
    readonly aria: string;
    readonly empty: string;
  };

  readonly serial: {
    readonly current: (value: string) => string;
    readonly currentOption: string;
    readonly rescan: string;
    readonly noneTitle: string;
    readonly noneText: string;
    readonly portsAria: string;
    readonly stable: (value: string) => string;
    readonly active: string;
    readonly baudrate: string;
    readonly baudrateHint: string;
    readonly test: string;
    readonly apply: string;
    readonly appliesImmediately: string;
  };

  readonly setupSteps: {
    readonly stepWelcome: string;
    readonly stepSerial: string;
    readonly stepBacnet: string;
    readonly stepMqtt: string;
    readonly stepSecurity: string;
    readonly stepDone: string;

    readonly welcomeCard: string;
    readonly welcomeText: string;
    readonly welcomeNeeds: string;
    readonly welcomeItem1: string;
    readonly welcomeItem2: string;
    readonly welcomeItem3: string;
    readonly welcomeItem4: string;
    readonly welcomeWarnTitle: string;
    readonly welcomeWarnText: string;
    readonly welcomeHelpLink: string;
    readonly welcomeRepeatTitle: string;
    readonly welcomeRepeatText: string;

    readonly serialCard: string;
    readonly serialHintTitle: string;
    readonly serialHintText: string;
    readonly serialNone: string;
    readonly serialHelpLink: string;
    readonly serialChoose: string;
    readonly serialRescan: string;
    readonly serialStable: (path: string) => string;
    readonly serialPassAll: string;

    readonly bacnetCard: string;
    readonly bacnetBaudrateHint: string;
    readonly bacnetMasterId: string;
    readonly bacnetMasterHint: string;
    readonly bacnetTest: string;
    readonly bacnetRecommended: string;
    readonly bacnetHelpLink: string;

    readonly mqttCard: string;
    readonly mqttLead: string;
    readonly mqttHost: string;
    readonly mqttPort: string;
    readonly mqttUser: string;
    readonly mqttPassword: string;
    readonly mqttPasswordHint: string;
    readonly mqttTls: string;
    readonly mqttTest: string;
    readonly mqttHelpLink: string;

    readonly securityCard: string;
    readonly securityLead: string;
    readonly securityUser: string;
    readonly securityUserHint: string;
    readonly securityPassword: string;
    readonly securityStrength: (value: number) => string;
    readonly securityRepeat: string;
    readonly securityMismatch: string;
    readonly passwordMinLength: string;
    readonly passwordClasses: string;
    readonly securityPort: string;
    readonly securityPortHint: (port: string) => string;
    readonly securityHelpLink: string;

    readonly doneCard: string;
    readonly doneApply: string;
    readonly doneRestarting: string;
    readonly doneWriting: string;
    readonly doneRunning: string;
    readonly donePublish: (topic: string) => string;
    readonly doneDashboard: string;
    readonly doneRepeatable: string;

    readonly checkPort: string;
    readonly checkBacnet: string;
    readonly checkMqtt: string;
    readonly checkAdmin: string;

    readonly hintSerialNotFound: string;
    readonly hintNoIam: string;
    readonly hintBusy: string;
    readonly hintPermissionDenied: string;
    readonly hintMqttUnreachable: string;
    readonly hintMqttAuthFailed: string;
    readonly hintMqttTlsFailed: string;
    readonly hintDefault: string;
  };
}
