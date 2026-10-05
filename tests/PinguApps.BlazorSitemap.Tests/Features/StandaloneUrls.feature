Feature: Published URLs outside component routing
  Fixed registrations and scoped providers supplement discovered pages without route templates.

  Scenario: Repeated fixed batches and multiple providers supplement Blazor pages
    Given an included page with route "/about"
    And trustworthy freshness for "/about" is "2026-09-01"
    And fixed standalone URL "/api/guide" with lastmod "2026-09-02"
    And fixed standalone URL "https://example.com/other/project" with lastmod "2026-09-03T12:00:00+01:00"
    And a standalone URL provider
    And published standalone URL "/reports/a" with lastmod "2026-09-04"
    And another URL provider supplies "/reports/b" with lastmod "2026-09-05"
    When the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path           |
      | /about         |
      | /api/guide     |
      | /other/project |
      | /reports/a     |
      | /reports/b     |
    And lastmod for "/other/project" is "2026-09-03T12:00:00+01:00"
    And the sitemap is valid UTF-8 XML with the protocol namespace and content type
    And standalone entries have no hreflang annotations

  Scenario: Application-relative paths respect a mount and supplied encoding
    Given public base URL "https://example.com/portal"
    And the application path base is "/portal"
    And fixed standalone URL "/guides/caf%C3%A9%20%26%20tea?edition=1&format=html" with lastmod "2026-09-01"
    When the sitemap is requested
    Then all canonical URLs start with "https://example.com/portal/"
    And the sitemap contains 1 canonical URLs
    And the XML contains escaped "&amp;format=html"

  Scenario: Equal standalone results deduplicate across batches and providers
    Given fixed standalone URL "/reports/a" with lastmod "2026-09-01"
    And fixed standalone URL "https://example.com/reports/a" with lastmod "2026-09-01"
    And a standalone URL provider
    And the standalone URL provider is registered again
    And published standalone URL "/reports/a" with lastmod "2026-09-01"
    When the sitemap is requested
    Then the sitemap contains 1 canonical URLs
    And the standalone provider was enumerated 1 times

  Scenario: Conflicting dates fail rather than choosing a provider
    Given fixed standalone URL "/reports/a" with lastmod "2026-09-01"
    And a standalone URL provider
    And published standalone URL "/reports/a" with lastmod "2026-09-02"
    When the sitemap is requested
    Then generation fails with diagnostic containing "Conflicting sitemap"

  Scenario: A manually supplied URL cannot claim an included component's URL
    Given an included page with route "/about"
    And trustworthy freshness for "/about" is "2026-09-01"
    And fixed standalone URL "/about" with lastmod "2026-09-01"
    When the sitemap is requested
    Then generation fails with diagnostic containing "Conflicting sitemap"

  Scenario Outline: Invalid fixed URLs fail before serving a document
    Given fixed standalone URL "<url>" with lastmod "2026-09-01"
    When the sitemap is requested
    Then generation fails with diagnostic containing "<diagnostic>"
    Examples:
      | url                                      | diagnostic          |
      | https://other.example/reports             | PublicBaseUrl       |
      | http://example.com/reports                | PublicBaseUrl       |
      | ftp://example.com/reports                 | PublicBaseUrl       |
      | https://user:password@example.com/reports | PublicBaseUrl       |
      | //other.example/reports                  | Invalid standalone  |
      | reports/a                               | PublicBaseUrl       |
      | /reports/a#section                       | Invalid standalone  |
      | /reports/a b                             | Invalid standalone  |
      | /reports/%GG                             | percent encoding    |
      | /reports/{slug}                          | Invalid standalone  |
      | /reports/<invalid>                       | Invalid standalone  |

  Scenario: Absolute URLs cannot escape the configured mount
    Given public base URL "https://example.com/portal"
    And fixed standalone URL "https://example.com/outside" with lastmod "2026-09-01"
    When the sitemap is requested
    Then generation fails with diagnostic containing "PublicBaseUrl"

  Scenario: Long URLs fail clearly
    Given a standalone URL with 2048 characters
    When the sitemap is requested
    Then generation fails with diagnostic containing "2048"

  Scenario Outline: Standalone URLs always require supplied dates
    Given <source> standalone URL "/reports/a" with lastmod "<none>"
    And a standalone URL provider
    When the sitemap is requested
    Then generation fails with diagnostic containing "Missing supplied lastmod"
    Examples:
      | source    |
      | fixed     |
      | published |

  Scenario: Runtime additions and removals respect cache expiry and invalidation
    Given a standalone URL provider
    And published standalone URL "/reports/old" with lastmod "2026-09-01"
    And the cache lifetime is 30 seconds
    When the sitemap is requested
    And the published standalone URLs become "/reports/new" dated "2026-09-02"
    And the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path         |
      | /reports/old |
    And the standalone provider was enumerated 1 times
    When 30 seconds elapse
    And the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path         |
      | /reports/new |
    When the published standalone URLs become "/reports/final" dated "2026-09-03"
    And the sitemap cache is invalidated
    And the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path           |
      | /reports/final |
    And lastmod for "/reports/final" is "2026-09-03"
    And the standalone provider was enumerated 3 times

  Scenario: Standalone entries use the same count partitions and real child endpoints
    Given fixed standalone URL "/reports/a" with lastmod "2026-09-01"
    And a standalone URL provider
    And published standalone URL "/reports/b" with lastmod "2026-09-02"
    And the URL limit is 1
    When the sitemap is requested
    Then a sitemap index is returned without lastmod
    When all child sitemaps are fetched
    Then all child limits hold and contain exactly 2 unique canonical URLs

  Scenario: Standalone entries use exact escaped byte boundaries
    Given fixed standalone URL "/reports/a?x=1&y=2" with lastmod "2026-09-01"
    And fixed standalone URL "/reports/b" with lastmod "2026-09-02"
    When the sitemap is requested
    And the sitemap is regenerated with a byte limit 1 bytes below its actual size
    Then a sitemap index is returned without lastmod
    When all child sitemaps are fetched
    Then all child limits hold and contain exactly 2 unique canonical URLs

  Scenario: An empty runtime catalogue creates no URLs
    Given a standalone URL provider
    When the sitemap is requested
    Then the sitemap contains 0 canonical URLs

  Scenario: Standalone providers never publish a partial failed query
    Given a standalone URL provider
    And published standalone URL "/reports/a" with lastmod "2026-09-01"
    And the standalone provider fails after enumeration
    When the sitemap is requested
    Then generation fails with diagnostic containing "Standalone publication query failed"

  Scenario: Register standalone sources after the sitemap setup
    When standalone registrations are attempted before sitemap setup
    Then all standalone registration attempts fail clearly
