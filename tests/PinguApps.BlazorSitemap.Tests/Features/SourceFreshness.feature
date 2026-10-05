Feature: Static source-change estimates survive builds
  Persistent sibling files track Razor and code-behind content without repeating routes.
  The packed package embeds these estimates so deployed apps need no source files.

  Scenario: Build stamps change only when the tracked inputs change or state is reset
    Given a fresh Razor project consuming the packed sitemap package
    When the source consumer is built
    Then its sitemap date matches its generated sibling state
    And its first source timestamp falls within the build interval
    When the source consumer is built
    Then its source state and sitemap date are unchanged
    When the Razor page content changes
    And the source consumer is built
    Then its source timestamp advances and matches the sitemap
    When the Razor code-behind content changes
    And the source consumer is built
    Then its source timestamp advances and matches the sitemap
    When the Razor code-behind is removed
    And the source consumer is built
    Then its source timestamp advances and matches the sitemap
    When the sibling source state is deleted
    And the source consumer is built
    Then its source timestamp advances and matches the sitemap
    When the source consumer is built with tracked inputs removed after compilation
    Then its sitemap date matches its generated sibling state

  Scenario: Corrupt history fails rather than silently inventing a replacement date
    Given a fresh Razor project consuming the packed sitemap package
    When the source consumer is built
    And the sibling source state is corrupted
    And the source consumer build is attempted
    Then the source build fails with a useful state-file diagnostic

  Scenario: Disabling estimates requires an application to supply accurate dates
    Given the packed Blazor consumer
    And source estimates are disabled for the consumer
    When the sitemap is requested
    Then generation fails with diagnostic containing "Missing trustworthy lastmod"

  Scenario: Supplied page dates take precedence over the embedded source estimate
    Given a fresh Razor project consuming the packed sitemap package
    And the source consumer supplies an accurate page date
    When the source consumer is built
    Then its sitemap uses the supplied page date instead of the source estimate
