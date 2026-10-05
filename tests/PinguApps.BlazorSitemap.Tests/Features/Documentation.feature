Feature: Documentation registration compiles against the package

  Scenario: The README setup Razor examples and typed providers compile with a real Razor app
    When the README setup snippet is compiled in a fresh packed consumer
    Then the documentation build succeeds
