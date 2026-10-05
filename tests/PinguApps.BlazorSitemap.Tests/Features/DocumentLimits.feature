Feature: Deterministic documents enforce both protocol limits

  Scenario: Exactly fifty thousand URLs fit in one document
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And 50000 published instances with parameter "slug" and value length 8
    When the sitemap is requested
    Then the sitemap contains 50000 canonical URLs

  Scenario: Fifty thousand and one URLs produce a valid index and reachable children
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And 50001 published instances with parameter "slug" and value length 8
    When the sitemap is requested
    Then a sitemap index is returned without lastmod
    When all child sitemaps are fetched
    Then all child limits hold and contain exactly 50001 unique canonical URLs

  Scenario: The real fifty megabyte boundary partitions fewer than fifty thousand URLs
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And 45000 published instances with parameter "slug" and value length 1200
    When the sitemap is requested
    Then a sitemap index is returned without lastmod
    When all child sitemaps are fetched
    Then all child limits hold and contain exactly 45000 unique canonical URLs

  Scenario: An exact serialized byte limit includes XML escaping and hreflang
    Given an included page with route "/{locale}/counter"
    And public base URL "https://example.com/a&b"
    And the application path base is "/a&b"
    And the page enables hreflang
    And English and Welsh locales with English x-default
    When the sitemap is requested
    And the sitemap is regenerated with a byte limit 0 bytes below its actual size
    Then the sitemap contains 2 canonical URLs
    And each localized entry has reciprocal English Welsh and x-default links
    When the sitemap is regenerated with a byte limit 1 bytes below its actual size
    Then a sitemap index is returned without lastmod
    When all child sitemaps are fetched
    Then all child limits hold and contain exactly 2 unique canonical URLs

  Scenario: A single oversized entry fails clearly
    Given an included page with route "/{locale}/counter"
    And the page enables hreflang
    And English and Welsh locales with English x-default
    And the byte limit is 256
    When the sitemap is requested
    Then generation fails with diagnostic containing "single sitemap entry exceeds the byte limit"

  Scenario: Limits cannot be configured above the protocol maximum
    Given an included page with route "/"
    And the URL limit is 50001
    When the sitemap is requested
    Then generation fails with diagnostic containing "limits must be"

  Scenario: Byte limits cannot be configured above the protocol maximum
    Given an included page with route "/"
    And the byte limit is 52428801
    When the sitemap is requested
    Then generation fails with diagnostic containing "limits must be"

  Scenario: Unrelated hash partitions are stable when a record is added
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And the URL limit is 10
    And 80 published instances with parameter "slug" and value length 8
    When the sitemap is requested
    And all child sitemaps are fetched
    Given published route instances:
      | slug       | lastmod |
      | new-record | 2026-09-01 |
    When the sitemap cache is invalidated
    And the sitemap is requested
    Then a sitemap index is returned without lastmod
    And the previously indexed unaffected children retain their URLs and bytes
    And all child limits hold and contain exactly 81 unique canonical URLs

  Scenario: The index refuses more than fifty thousand child documents
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And the URL limit is 1
    And 50001 published instances with parameter "slug" and value length 8
    When the sitemap is requested
    Then generation fails with diagnostic containing "index exceeds 50,000"

  Scenario: Child URLs preserve the configured origin and path base
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And the URL limit is 1
    And public base URL "https://example.com/portal"
    And the application path base is "/portal"
    And 3 published instances with parameter "slug" and value length 8
    When the sitemap is requested
    Then a sitemap index is returned without lastmod
    And all canonical URLs start with "https://example.com/portal/sitemap-"
    When all child sitemaps are fetched
    Then all child limits hold and contain exactly 3 unique canonical URLs

  Scenario: The index enforces its own fifty megabyte limit below its child count limit
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And the URL limit is 1
    And the public base URL has a path of 1500 characters
    And 35000 published instances with parameter "slug" and value length 8
    When the sitemap is requested
    Then generation fails with diagnostic containing "index exceeds 52,428,800"

  Scenario: Child URLs must also satisfy the protocol URL length limit
    Given an included page with route "/blog/{slug}"
    And the page uses a runtime provider
    And the URL limit is 1
    And the public base URL has a path of 2010 characters
    And 5000 published instances with parameter "slug" and value length 8
    When the sitemap is requested
    Then generation fails with diagnostic containing "child URL must be shorter than 2048"
