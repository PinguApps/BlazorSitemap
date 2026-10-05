Feature: Publication freshness and cache lifecycle

  Scenario: Freshness providers can be registered for several different pages
    Given an included page with route "/blog"
    And the page has dynamic content
    And trustworthy freshness for "/blog" is "2026-09-01"
    And an included page with route "/catalogue"
    And the page has dynamic content
    And trustworthy freshness for "/catalogue" is "2026-09-02"
    When the sitemap is requested
    Then lastmod for "/blog" is "2026-09-01"
    And lastmod for "/catalogue" is "2026-09-02"

  Scenario: Duplicate freshness registration for a page fails clearly
    Given an included page with route "/blog"
    And a second freshness provider is registered for the same page
    When the sitemap is requested
    Then generation fails with diagnostic containing "already registered for"

  Scenario: Deployment freshness supports date precision independently of file or build times
    Given an included page with route "/about"
    And trustworthy freshness for "/about" is "2026-08-04"
    And lastmod is required
    When the sitemap is requested
    Then lastmod for "/about" is "2026-08-04"

  Scenario: Collection freshness preserves deletion events even when no articles remain
    Given an included page with route "/blog"
    And the page has dynamic content
    And trustworthy freshness for "/blog" is "2026-09-01T10:00:00Z"
    And an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And published route instances:
      | slug | lastmod                   |
      | a    | 2026-09-01T10:00:00Z       |
    When the sitemap is requested
    And all published instances are removed
    And the collection publication time for "/blog" changes to "2026-10-02T14:00:00Z"
    And the sitemap cache is invalidated
    And the sitemap is requested
    Then the sitemap contains 1 canonical URLs
    And lastmod for "/blog" is "2026-10-02T14:00:00Z"

  Scenario: Cache expiry has a predictable exact boundary
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And the cache lifetime is 30 seconds
    And published route instances:
      | slug | lastmod |
      | a    | 2026-09-01 |
    When the sitemap is requested
    And all published instances are removed
    And 29 seconds elapse
    And the sitemap is requested
    Then the sitemap contains 1 canonical URLs
    And the provider has been read 1 times
    When 1 seconds elapse
    And the sitemap is requested
    Then the sitemap contains 0 canonical URLs
    And the provider has been read 2 times

  Scenario: A zero lifetime refreshes each request
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And the cache lifetime is 0 seconds
    When the sitemap is requested
    And the sitemap is requested
    Then the provider has been read 2 times

  Scenario: Invalidation during generation is not lost
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    When the sitemap is requested
    And the provider invalidates the cache during its next read
    And the sitemap is requested
    And the sitemap is requested
    Then the provider has been read 3 times

  Scenario: A failed provider does not publish a partial snapshot and can recover
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And published route instances:
      | slug | lastmod |
      | a    | 2026-09-01 |
    When the sitemap is requested
    And the provider fails its next read
    And the sitemap is requested
    Then generation fails with diagnostic containing "Publication query failed"
    When the provider recovers
    And the sitemap is requested
    Then the sitemap contains 1 canonical URLs
    And the provider has been read 3 times

  Scenario: A dynamic fixed page requires collection freshness
    Given an included page with route "/blog"
    And the page has dynamic content
    When the sitemap is requested
    Then generation fails with diagnostic containing "Missing trustworthy lastmod"

  Scenario: Strict static freshness fails when neither a stamp nor a provider exists
    Given an included page with route "/"
    And lastmod is required
    When the sitemap is requested
    Then generation fails with diagnostic containing "Missing trustworthy lastmod"
