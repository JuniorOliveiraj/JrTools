# Requirements Document

## Introduction

O BServer Connection Tester é uma funcionalidade para a aplicação JrTools que permite aos desenvolvedores testar conectividade com servidores BServer e descobrir sistemas disponíveis dinamicamente. Esta funcionalidade integra-se aos padrões de configuração existentes do JrTools e oferece suporte opcional ao DLL Benner.Tecnologia.BServer.Clients.dll, garantindo funcionamento mesmo quando a biblioteca não estiver disponível.

## Glossary

- **BServer**: Servidor de aplicação da Benner Tecnologia que hospeda sistemas empresariais
- **BConnectionClient**: Classe do DLL Benner.Tecnologia.BServer.Clients.dll responsável pela conexão com BServer
- **Sistema**: Instância de aplicação empresarial hospedada no BServer (ex: "SISTEMA_RH", "SISTEMA_FINANCEIRO")  
- **DiretorioBinarios**: Configuração existente no JrTools que aponta para o diretório de binários da Benner
- **Connection_Tester**: Componente principal que gerencia conectividade com BServer
- **Configuration_Helper**: Serviço responsável por persistir configurações de conectividade
- **JrTools**: Aplicação WinUI C# para ferramentas de desenvolvimento

## Requirements

### Requirement 1: Optional DLL Loading

**User Story:** As a developer, I want the connection tester to work even when the BServer DLL is not available, so that I can use JrTools in environments without the Benner SDK.

#### Acceptance Criteria

1. WHEN the BServer DLL does not exist at the configured path, THE Connection_Tester SHALL display an informative message indicating DLL unavailability
2. WHEN the BServer DLL is not available, THE Connection_Tester SHALL disable connection functionality gracefully without crashing the application
3. THE Connection_Tester SHALL attempt to load the DLL from the path `{DiretorioBinarios}/delphi/Benner.Tecnologia.BServer.Clients.dll`
4. WHEN DLL loading fails due to missing dependencies, THE Connection_Tester SHALL log the specific error and provide user guidance
5. THE Connection_Tester SHALL validate DiretorioBinarios configuration before attempting DLL loading

### Requirement 2: BServer Connection Management

**User Story:** As a developer, I want to connect to BServer and retrieve available systems, so that I can validate server connectivity and discover system configurations dynamically.

#### Acceptance Criteria

1. WHEN a valid server address is provided, THE Connection_Tester SHALL establish connection using BConnectionClient.Connect method
2. WHEN connection is successful, THE Connection_Tester SHALL retrieve system list using GetSystemNames method with ArrayList parameter
3. WHEN connection timeout occurs (default 30 seconds), THE Connection_Tester SHALL abort connection attempt and return timeout error
4. THE Connection_Tester SHALL use port 2000 as default BServer connection port unless user specifies otherwise
5. WHEN server is unreachable, THE Connection_Tester SHALL return meaningful error message indicating network connectivity issues
6. THE Connection_Tester SHALL validate server address format before attempting connection
7. FOR ALL successful connections, THE Connection_Tester SHALL return system names as string array converted from ArrayList

### Requirement 3: Configuration Integration

**User Story:** As a developer, I want BServer connection settings integrated with existing JrTools configuration patterns, so that settings persist consistently with other application configurations.

#### Acceptance Criteria

1. THE Configuration_Helper SHALL persist BServer connection settings to JSON file in %LocalAppData%/JrTools directory
2. THE Configuration_Helper SHALL reuse existing ConfigHelper pattern for configuration management
3. WHEN configuration is saved, THE Configuration_Helper SHALL follow the same async/await pattern used by ConfiguracaoRelatoriosHelper
4. THE Configuration_Helper SHALL validate required configuration fields before attempting connection
5. THE Configuration_Helper SHALL integrate with existing DiretorioBinarios configuration from ConfiguracoesdataObject
6. WHEN configuration file is missing, THE Configuration_Helper SHALL create default configuration with empty server field

### Requirement 4: User Interface Integration

**User Story:** As a developer, I want BServer connection testing integrated into JrTools UI, so that I can easily test connections and view results within the existing application interface.

#### Acceptance Criteria

1. THE UI_Component SHALL display connection status (Connected, Disconnected, Error) with appropriate visual indicators
2. WHEN connection test succeeds, THE UI_Component SHALL display list of available systems in a readable format
3. WHEN connection test fails, THE UI_Component SHALL display specific error message in user-friendly format
4. THE UI_Component SHALL provide input fields for server address and optional port configuration
5. THE UI_Component SHALL follow existing JrTools WinUI styling and layout patterns
6. WHEN DLL is unavailable, THE UI_Component SHALL disable connection controls and show DLL status message
7. THE UI_Component SHALL provide "Test Connection" button that triggers connection validation

### Requirement 5: Error Handling and Resilience

**User Story:** As a developer, I want comprehensive error handling for connection failures, so that I can troubleshoot BServer connectivity issues effectively.

#### Acceptance Criteria

1. WHEN DLL file is not found, THE Connection_Tester SHALL return error message "BServer DLL not found at configured path"
2. WHEN DLL dependencies are missing, THE Connection_Tester SHALL return error message indicating required runtime components
3. WHEN network connection fails, THE Connection_Tester SHALL return error message distinguishing between timeout and unreachable server
4. WHEN authentication fails, THE Connection_Tester SHALL return error message indicating invalid credentials or access denied
5. WHEN BServer returns invalid response, THE Connection_Tester SHALL return error message indicating server communication issues
6. THE Connection_Tester SHALL log all connection attempts and errors for debugging purposes
7. WHEN multiple connection attempts occur simultaneously, THE Connection_Tester SHALL queue requests to prevent resource conflicts

### Requirement 6: Configuration Persistence

**User Story:** As a developer, I want my BServer connection settings saved automatically, so that I don't need to re-enter server information on each use.

#### Acceptance Criteria

1. THE Configuration_Helper SHALL automatically save server address when connection test is performed
2. THE Configuration_Helper SHALL restore last used server configuration when application starts
3. WHEN user changes server address, THE Configuration_Helper SHALL save changes immediately using TextChanged event pattern
4. THE Configuration_Helper SHALL maintain connection history list for recently used servers  
5. THE Configuration_Helper SHALL persist connection timeout settings with default value of 30 seconds
6. WHEN configuration file becomes corrupted, THE Configuration_Helper SHALL recreate default configuration without data loss

### Requirement 7: System Discovery and Validation

**User Story:** As a developer, I want to see which systems are available on a BServer, so that I can validate system configurations and select appropriate targets for deployment.

#### Acceptance Criteria

1. WHEN GetSystemNames executes successfully, THE Connection_Tester SHALL convert ArrayList results to string array
2. THE Connection_Tester SHALL display system names in alphabetical order for consistent user experience
3. WHEN no systems are available, THE Connection_Tester SHALL display message "No systems found on server"
4. THE Connection_Tester SHALL validate that returned system names contain only valid characters for system identifiers
5. WHEN system list is large (>50 systems), THE Connection_Tester SHALL implement scrolling or pagination in UI display
6. THE Connection_Tester SHALL cache system list results for 5 minutes to avoid repeated server queries
7. FOR ALL discovered systems, THE Connection_Tester SHALL provide option to copy system name to clipboard