# Implementation Plan: BServer Connection Tester

## Overview

This implementation plan creates a BServer Connection Tester feature for JrTools that enables developers to test connectivity with BServer instances and discover available systems. The implementation follows established JrTools patterns including WinUI page navigation, JSON configuration persistence, and graceful error handling with optional DLL loading.

## Tasks

- [ ] 1. Create data models and DTOs
  - [x] 1.1 Create BServerConfigDto in Dto folder
    - Define configuration data structure with server address, port, timeout settings
    - Include connection history and system caching properties 
    - Follow existing DTO patterns from ConfiguracoesdataObject and ConfiguracaoRelatoriosRh
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [x] 1.2 Create connection result and status models
    - Implement ConnectionResult class with success status, error messages, and system list
    - Implement ConnectionErrorType enum for different failure categories
    - Implement ConnectionStatusEventArgs for status change notifications
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

- [ ] 2. Implement configuration helper service
  - [x] 2.1 Create BServerConfigHelper following ConfiguracaoRelatoriosHelper pattern
    - Implement LerAsync() method for configuration loading from %LocalAppData%/JrTools
    - Implement SalvarAsync() method using JSON serialization with WriteIndented
    - Follow exact async/await patterns from existing ConfiguracaoRelatoriosHelper
    - _Requirements: 3.1, 3.2, 3.3, 3.6_

  - [-] 2.2 Write unit tests for BServerConfigHelper
    - Test configuration loading with missing file scenarios
    - Test configuration saving and persistence
    - Test error handling for corrupted configuration files
    - _Requirements: 3.1, 3.2, 3.3, 3.6_

- [ ] 3. Create connection validation service
  - [~] 3.1 Create ConnectionValidator class for input validation
    - Implement server address format validation (IPv4, hostname, FQDN)
    - Implement port range validation (1-65535) 
    - Implement timeout range validation (1-300 seconds)
    - _Requirements: 2.6, 5.1, 5.2, 5.3_

  - [~] 3.2 Write unit tests for ConnectionValidator
    - Test various server address formats and edge cases
    - Test port and timeout boundary conditions
    - Test validation error message generation
    - _Requirements: 2.6, 5.1, 5.2, 5.3_

- [ ] 4. Implement BServer connection service
  - [~] 4.1 Create BServerConnectionService class
    - Implement optional DLL loading using Assembly.LoadFrom with try-catch
    - Implement TestConnectionAsync method with timeout handling
    - Implement GetAvailableSystemsAsync method with ArrayList conversion
    - Add IsDllAvailable and DllStatus properties for UI binding
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5, 2.7_

  - [~] 4.2 Add comprehensive error handling to connection service
    - Implement specific error types for DLL not found, dependency missing, network timeout
    - Add connection attempt queuing to prevent resource conflicts
    - Implement system discovery caching with 5-minute expiry
    - Add detailed logging for all connection attempts and errors
    - _Requirements: 1.1, 1.2, 1.4, 2.5, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 7.6_

  - [~] 4.3 Integrate with existing DiretorioBinarios configuration
    - Load ConfiguracoesdataObject using ConfigHelper.LerConfiguracoesAsync
    - Construct DLL path from DiretorioBinarios + "/delphi/Benner.Tecnologia.BServer.Clients.dll"
    - Validate DiretorioBinarios configuration before DLL loading
    - _Requirements: 1.3, 1.5, 3.5_

  - [~] 4.4 Write unit tests for BServerConnectionService
    - Mock BConnectionClient for connection testing scenarios
    - Test DLL loading with different availability scenarios
    - Test error handling paths and timeout behavior
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5_

- [~] 5. Checkpoint - Core services validation
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Create BServer Connection UI page
  - [~] 6.1 Create BServerConnectionPage.xaml following JrTools WinUI patterns
    - Design layout with server address input, port input, test connection button
    - Add connection status indicator (Connected/Disconnected/Error) with visual indicators
    - Add available systems list display with scrolling support
    - Follow existing styling patterns from ConfiguracoesPage and ImportadorRelatoriosPage
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 7.2, 7.5_

  - [~] 6.2 Create BServerConnectionPage.xaml.cs code-behind
    - Implement page initialization with NavigationCacheMode.Required
    - Add server address and port TextChanged event handlers for auto-save
    - Implement test connection button click handler with loading states
    - Add connection status updates and error message display using InfoBar
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 6.1, 6.2, 6.3_

  - [~] 6.3 Add DLL status display and graceful degradation
    - Show DLL availability status message in UI
    - Disable connection controls when DLL is unavailable
    - Display informative messages for different DLL loading failures
    - _Requirements: 1.1, 1.2, 4.6_

  - [~] 6.4 Implement system discovery display functionality
    - Display available systems in alphabetical order
    - Handle "No systems found" scenario with user-friendly message
    - Add pagination or scrolling for large system lists (>50 systems)
    - Implement copy to clipboard functionality for system names
    - _Requirements: 7.1, 7.2, 7.3, 7.5, 7.7_

  - [~] 6.5 Write UI component tests
    - Test InfoBar error message display functionality
    - Test input validation feedback mechanisms
    - Test loading state management during connections
    - _Requirements: 4.1, 4.2, 4.3, 4.6_

- [ ] 7. Integrate with MainWindow navigation
  - [~] 7.1 Add BServerConnectionPage instance to MainWindow.xaml.cs
    - Create private field for page instance following existing pattern
    - Add navigation case in NavView_SelectionChanged method
    - _Requirements: 4.5_

  - [~] 7.2 Add navigation menu item to MainWindow.xaml
    - Add NavigationViewItem with appropriate icon and content
    - Use meaningful tag for navigation routing
    - Position appropriately in existing menu structure
    - _Requirements: 4.5_

- [ ] 8. Implement error handling and user feedback
  - [~] 8.1 Add comprehensive error message handling in UI
    - Display specific error messages for each ConnectionErrorType
    - Implement user-friendly messages for technical errors
    - Add error recovery suggestions for common issues
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [~] 8.2 Add connection attempt validation and feedback
    - Validate all input fields before connection attempt
    - Provide real-time validation feedback for server address format
    - Show connection progress and timeout countdown
    - _Requirements: 2.6, 4.2, 4.3, 5.1_

- [ ] 9. Final integration and testing
  - [~] 9.1 Wire all components together
    - Connect BServerConnectionService to BServerConnectionPage
    - Integrate BServerConfigHelper for configuration persistence
    - Connect ConnectionValidator for input validation
    - Test complete user workflow from UI to service to configuration
    - _Requirements: All requirements integrated_

  - [~] 9.2 Write integration tests
    - Test configuration persistence with temporary directories
    - Test UI navigation integration with MainWindow
    - Test service layer integration with mocked dependencies
    - _Requirements: 3.1, 3.2, 3.3, 4.5_

- [~] 10. Final checkpoint - Comprehensive validation
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation and user feedback
- The implementation follows established JrTools patterns for consistency
- DLL loading is implemented with graceful degradation when Benner SDK is unavailable
- Configuration integration leverages existing JrTools infrastructure
- Error handling provides comprehensive user guidance for troubleshooting