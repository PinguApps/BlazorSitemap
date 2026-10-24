Feature: One trailing slash policy for published page URLs

  Scenario: The packed consumer defaults to slashless pages
    Given the packed Blazor consumer
    When the sitemap is requested
    Then all page locations and language links use trailing slash policy "omit"
    And the sitemap contains 13 canonical URLs
    And every advertised page renders successfully

  Scenario: The packed consumer opts into trailing slashes
    Given the packed Blazor consumer
    And the consumer appends trailing slashes
    When the sitemap is requested
    Then all page locations and language links use trailing slash policy "append"
    And the sitemap contains 13 canonical URLs
    And every advertised page renders successfully

  Scenario: The packed consumer preserves its mount with the default policy
    Given the packed Blazor consumer
    And public base URL "https://public.example/portal"
    And the application path base is "/portal"
    When the sitemap is requested
    Then all page locations and language links use trailing slash policy "omit"
    And the sitemap contains 13 canonical URLs
    And the sitemap contains exactly these canonical paths:
      | path                           |
      | /portal                        |
      | /portal/auto                   |
      | /portal/about                  |
      | /portal/blog                   |
      | /portal/en-gb/counter          |
      | /portal/cy-gb/counter          |
      | /portal/en-gb/blog/welcome     |
      | /portal/cy-gb/blog/croeso      |
      | /portal/en-gb/blog/the-coast   |
      | /portal/library                |
      | /portal/api/guide              |
      | /portal/api/articles/welcome   |
      | /portal/api/articles/the-coast |
    And every advertised page renders successfully

  Scenario: The packed consumer preserves its mount with appended slashes
    Given the packed Blazor consumer
    And the consumer appends trailing slashes
    And public base URL "https://public.example/portal/"
    And the application path base is "/portal"
    When the sitemap is requested
    Then all page locations and language links use trailing slash policy "append"
    And all canonical URLs start with "https://public.example/portal/"
    And the sitemap contains 13 canonical URLs
    And every advertised page renders successfully

  Scenario Outline: Both modes merge standalone slash variants while preserving the query
    Given public base URL "https://example.com/portal"
    And the application path base is "/portal"
    And trailing slash policy "<mode>"
    And fixed standalone URL "/reports/caf%C3%A9%2Ftea///?edition=1&next=/" with lastmod "2026-09-01"
    And a standalone URL provider
    And published standalone URL "https://example.com/portal/reports/caf%C3%A9%2Ftea?edition=1&next=/" with lastmod "2026-09-01"
    When the sitemap is requested
    Then the sitemap contains exactly these page locations:
      | url                                                                           |
      | https://example.com/portal/reports/caf%C3%A9%2Ftea<slash>?edition=1&next=/        |
    And the XML contains escaped "&amp;next=/"
    Examples:
      | mode   | slash |
      | omit   |       |
      | append | /     |

  Scenario: An omitted setting removes standalone slashes without changing the domain root or query
    Given fixed standalone URL "/reports///?next=/" with lastmod "2026-09-01"
    And fixed standalone URL "/?next=/" with lastmod "2026-09-01"
    When the sitemap is requested
    Then the sitemap contains exactly these page locations:
      | url                                 |
      | https://example.com/reports?next=/   |
      | https://example.com/?next=/          |

  Scenario Outline: Slash variants cannot bypass component ownership
    Given an included page with route "/about/"
    And trailing slash policy "<mode>"
    And fixed standalone URL "/about///" with lastmod "2026-09-01"
    When the sitemap is requested
    Then generation fails with diagnostic containing "Conflicting sitemap"
    Examples:
      | mode   |
      | omit   |
      | append |

  Scenario: The freshness provider receives the chosen canonical address
    Given an included page with route "/about"
    And trailing slash policy "append"
    And lastmod is required
    And trustworthy freshness for "/about/" is "2026-09-01"
    When the sitemap is requested
    Then lastmod for "/about/" is "2026-09-01"

  Scenario Outline: Page policy preserves mounted sitemap document endpoints and exact byte partitions
    Given public base URL "https://example.com/portal"
    And the application path base is "/portal"
    And trailing slash policy "<mode>"
    And an included page with route "/{locale}/counter/"
    And the page enables hreflang
    And English and Welsh locales with English x-default
    And fixed standalone URL "/reports///?next=/" with lastmod "2026-09-01"
    When the sitemap is requested
    Then all page locations and language links use trailing slash policy "<mode>"
    When the sitemap is regenerated with a byte limit 1 bytes below its actual size
    Then a sitemap index is returned without lastmod
    When all child sitemaps are fetched
    Then all child limits hold and contain exactly 3 unique canonical URLs
    And child document addresses end in xml without a slash
    And all page locations and language links use trailing slash policy "<mode>"
    Examples:
      | mode   |
      | omit   |
      | append |

  Scenario: Appending a slash still enforces the canonical URL length limit
    Given trailing slash policy "append"
    And a standalone page URL of 2047 characters
    When the sitemap is requested
    Then generation fails with diagnostic containing "2048"

  Scenario Outline: Absolute mounted home URLs accept and merge either spelling
    Given public base URL "https://example.com/portal/"
    And the application path base is "/portal"
    And trailing slash policy "<mode>"
    And fixed standalone URL "https://example.com/portal?next=/" with lastmod "2026-09-01"
    And a standalone URL provider
    And published standalone URL "/?next=/" with lastmod "2026-09-01"
    When the sitemap is requested
    Then the sitemap contains exactly these page locations:
      | url                                           |
      | https://example.com/portal<slash>?next=/        |
    Examples:
      | mode   | slash |
      | omit   |       |
      | append | /     |
