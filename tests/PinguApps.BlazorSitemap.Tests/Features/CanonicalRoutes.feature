Feature: Canonical routes discovered from compiled components
  Only explicitly included public canonical URLs are advertised.

  Scenario: Fixed page needs no route registration and no fabricated freshness
    Given an included page with route "/"
    When the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path |
      | /    |
    And the sitemap is valid UTF-8 XML with the protocol namespace and content type
    And no lastmod is fabricated
    And an unknown child endpoint returns not found

  Scenario: Localized variants are separately canonical and reciprocal
    Given an included page with route "/{locale}/counter"
    And the page enables hreflang
    And English and Welsh locales with English x-default
    When the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path           |
      | /en-gb/counter |
      | /cy-gb/counter |
    And each localized entry has reciprocal English Welsh and x-default links
    And no lastmod is fabricated

  Scenario: Many finite values need no repeated route declarations
    Given an included page with route "/{locale}/public"
    And 60 finite values for "locale"
    When the sitemap is requested
    Then the sitemap contains 60 canonical URLs

  Scenario: An explicit canonical template excludes an old alias
    Given an included page with route "/about"
    And the page also has alias "/old-about"
    And the canonical route index is 0
    When the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path   |
      | /about |

  Scenario: Multiple routes require the smallest explicit canonical choice
    Given an included page with route "/about"
    And the page also has alias "/old-about"
    When the sitemap is requested
    Then generation fails with diagnostic containing "choose CanonicalRoute"

  Scenario: Canonical choices must refer to a real page template
    Given an included page with route "/about"
    And the canonical route index is 7
    When the sitemap is requested
    Then generation fails with diagnostic containing "choose CanonicalRoute"

  Scenario: Excluded pages are not discovered
    Given an included page with route "/public"
    And an excluded page with route "/login"
    And an excluded page with route "/private"
    And an excluded page with route "/redirect"
    When the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path    |
      | /public |

  Scenario: Including an authorized component is a checkable conflict
    Given an included page with route "/private"
    And the page requires authorization
    When the sitemap is requested
    Then generation fails with diagnostic containing "authorized component"

  Scenario: Unbounded route parameters require a provider
    Given an included page with route "/blog/{slug}"
    When the sitemap is requested
    Then generation fails with diagnostic containing "unresolved parameter 'slug'"

  Scenario: A missing locale source is diagnosed
    Given an included page with route "/{locale}/counter"
    When the sitemap is requested
    Then generation fails with diagnostic containing "unresolved parameter 'locale'"

  Scenario Outline: Finite values are nonempty and unique
    Given an included page with route "/{locale}/counter"
    And finite values for "locale" are "<values>"
    When the sitemap is requested
    Then generation fails with diagnostic containing "nonempty, unique values"
    Examples:
      | values       |
      | en-gb,en-gb  |
      | en-gb,EN-GB  |
      |              |

  Scenario: Duplicate language annotations fail clearly
    Given an included page with route "/{locale}/counter"
    And locales with duplicate language codes
    When the sitemap is requested
    Then generation fails with diagnostic containing "unique language"

  Scenario: x-default must be explicitly registered
    Given an included page with route "/{locale}/counter"
    And an unregistered x-default locale
    When the sitemap is requested
    Then generation fails with diagnostic containing "x-default"

  Scenario: Hreflang needs an actual locale parameter
    Given an included page with route "/counter"
    And the page enables hreflang
    And English and Welsh locales with English x-default
    When the sitemap is requested
    Then generation fails with diagnostic containing "configured locale parameter"

  Scenario: Optional parameter omission is an explicit finite value
    Given an included page with route "/archive/{year:int?}"
    And finite values for "year" are "<null>,2026"
    When the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path          |
      | /archive      |
      | /archive/2026 |

  Scenario: Optional parameters are not silently guessed
    Given an included page with route "/archive/{year:int?}"
    When the sitemap is requested
    Then generation fails with diagnostic containing "explicit null choice"

  Scenario Outline: Built-in constraints accept correctly typed route values
    Given an included page with route "/value/{id:<constraint>}"
    And finite values for "id" are "<value>"
    When the sitemap is requested
    Then the sitemap contains 1 canonical URLs
    Examples:
      | constraint | value                                |
      | int        | -42                                  |
      | long       | 9223372036854775807                  |
      | bool       | true                                 |
      | guid       | 00001111-aaaa-2222-bbbb-3333cccc4444     |
      | decimal    | 1.25                                 |
      | float      | 1.25                                 |
      | double     | 1.25                                 |
      | datetime   | 2026-10-01                           |
      | nonfile    | article                              |

  Scenario Outline: Constraint violations fail instead of advertising dead URLs
    Given an included page with route "/value/{id:<constraint>}"
    And finite values for "id" are "<value>"
    When the sitemap is requested
    Then generation fails with diagnostic containing "violates constraint"
    Examples:
      | constraint | value     |
      | int        | article   |
      | bool       | perhaps   |
      | nonfile    | app.css   |

  Scenario: Unsupported constraints have a useful diagnostic
    Given an included page with route "/value/{id:custom}"
    And finite values for "id" are "42"
    When the sitemap is requested
    Then generation fails with diagnostic containing "Unsupported Blazor sitemap constraint"

  Scenario: Decoded values are encoded once and XML is escaped
    Given an included page with route "/search/{term}"
    And finite values for "term" are "café & tea"
    When the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path                         |
      | /search/caf%C3%A9%20%26%20tea |
    And the sitemap is valid UTF-8 XML with the protocol namespace and content type

  Scenario: XML escaping accounts for an ampersand in the configured path base
    Given an included page with route "/about"
    And public base URL "https://example.com/a&b"
    When the sitemap is requested
    Then the XML contains escaped "a&amp;b/about"

  Scenario: Encoding preserves distinct composed and decomposed application route values
    Given an included page with route "/search/{term}"
    And finite values for "term" are "café,café"
    When the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path                     |
      | /search/caf%C3%A9         |
      | /search/cafe%CC%81        |

  Scenario: Catch-all values preserve path boundaries while encoding segments
    Given an included page with route "/docs/{*path}"
    And finite values for "path" are "guide/first steps"
    When the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path                      |
      | /docs/guide/first%20steps |

  Scenario Outline: Dangerous or ambiguous route segments fail clearly
    Given an included page with route "/docs/{path}"
    And finite values for "path" are "<value>"
    When the sitemap is requested
    Then generation fails with diagnostic containing "canonical route segment"
    Examples:
      | value       |
      | ..          |
      | one/two     |
      | one\two     |

  Scenario: Complex segments are deliberately rejected
    Given an included page with route "/file/{name}.{ext}"
    And finite values for "name" are "hello"
    And finite values for "ext" are "txt"
    When the sitemap is requested
    Then generation fails with diagnostic containing "complex route segments"

  Scenario: URLs use the configured origin and path base regardless of Host
    Given an included page with route "/about"
    And public base URL "https://public.example/portal/"
    And the application path base is "/portal"
    When the sitemap is requested
    And the incoming Host header is "attacker.example"
    And the sitemap is requested
    Then all canonical URLs start with "https://public.example/portal/"
    And the sitemap contains exactly these canonical paths:
      | path          |
      | /portal/about |

  Scenario Outline: Invalid public base URLs fail during registration
    Given an included page with route "/"
    And public base URL "<url>"
    When the sitemap is requested
    Then generation fails with diagnostic containing "PublicBaseUrl"
    Examples:
      | url                              |
      | /relative                        |
      | ftp://example.com                |
      | https://user:password@example.com |
      | https://example.com/?query=true   |
      | https://example.com/#fragment     |
      | https://example.com/a b           |

  Scenario: Hreflang uses the named parameter and preserves other finite values
    Given an included page with route "/{ItemId:int}/{culture}/counter"
    And the page enables hreflang
    And finite values for "ItemId" are "42,43"
    And locale parameter "culture" uses English and Welsh
    When the sitemap is requested
    Then the sitemap contains 4 canonical URLs
    And alternates for each item stay within that item's group
