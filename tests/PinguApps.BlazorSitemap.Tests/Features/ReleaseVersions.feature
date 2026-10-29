Feature: Releases always use full semantic versions
  Draft publication and NuGet publication share strict SemVer validation.

  Scenario Outline: Valid release tags retain all version components
    When release tag "<tag>" is validated
    Then the package version is "<version>"
    Examples:
      | tag                         | version                    |
      | v2.0.1                      | 2.0.1                      |
      | v0.0.0                      | 0.0.0                      |
      | v2.10.12-rc.1               | 2.10.12-rc.1               |
      | v2.0.1-beta+build.001        | 2.0.1-beta+build.001        |

  Scenario Outline: Incomplete and malformed versions fail before publishing
    When release tag "<tag>" is validated
    Then release validation fails with "Release tag must be v<SemVer>"
    Examples:
      | tag              |
      | v2               |
      | v2.0             |
      | 2.0.1            |
      | V2.0.1           |
      | v02.0.1          |
      | v2.00.1          |
      | v2.0.01          |
      | v2.0.1.0         |
      | v2.0.1-rc..1     |
      | v2.0.1-01        |
      | v2.0.1-          |
      | v2.0.1+          |
      | v2.0.1+build..1  |

  Scenario: Trailing newlines cannot bypass validation
    When a release tag with a trailing newline is validated
    Then release validation fails with "Release tag must be v<SemVer>"

  Scenario: A release name and tag show the same complete version
    When release tag "v2.0.1" with name "v2.0.1" is validated
    Then the package version is "2.0.1"

  Scenario Outline: Abbreviated or mismatched release names are rejected
    When release tag "v2.0.1" with name "<name>" is validated
    Then release validation fails with "Release name must exactly match"
    Examples:
      | name   |
      | v2     |
      | v2.0.0 |
      | V2.0.1 |
      |        |

  Scenario: Scheduled publication accepts full stable versions
    When scheduled release tag "v2.0.1" is validated
    Then the package version is "2.0.1"

  Scenario: Scheduled publication continues to reject prereleases
    When scheduled release tag "v2.0.1-rc.1" is validated
    Then release validation fails with "Scheduled releases require a stable"
