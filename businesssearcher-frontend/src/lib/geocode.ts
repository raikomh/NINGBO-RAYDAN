import type { Address } from '@/lib/types';

// Country name (as typed by the user) → ISO 3166-1 alpha-2 code.
// Used to constrain Nominatim to the right country so ambiguous street/neighborhood
// names (e.g. "Carolina", "Garrido") don't resolve to a place in another country.
const COUNTRY_CODES: Record<string, string> = {
  cuba: 'cu',
  colombia: 'co',
  mexico: 'mx', méxico: 'mx',
  venezuela: 've',
  'republica dominicana': 'do', 'república dominicana': 'do',
  'estados unidos': 'us', usa: 'us', 'united states': 'us',
  espana: 'es', españa: 'es', spain: 'es',
  argentina: 'ar', chile: 'cl', ecuador: 'ec', bolivia: 'bo',
  peru: 'pe', perú: 'pe', uruguay: 'uy', paraguay: 'py',
  panama: 'pa', panamá: 'pa', 'costa rica': 'cr', guatemala: 'gt',
  honduras: 'hn', nicaragua: 'ni', 'el salvador': 'sv', 'puerto rico': 'pr',
};

export function countryCode(country?: string | null): string | undefined {
  if (!country) return undefined;
  return COUNTRY_CODES[country.trim().toLowerCase()];
}

// Cuban addresses use informal notation that geocoders can't parse:
// "entre X y Y" / "e/ X y Y", "esq. a Z", "% ", and apartment/number suffixes.
// Keep only the street name so Nominatim can match it within the city.
export function cleanStreet(street?: string | null): string | undefined {
  if (!street) return undefined;
  let s = street.trim();
  // Cut at the first "between/corner" marker: entre, e/, esq(uina), %
  s = s.split(/\s+(?:entre|e\/|esquina|esq\.?|%)\b/i)[0];
  // Drop apartment / number / floor / building tokens and whatever follows
  s = s.replace(/\b(?:apto|apartamento|apt|no\.?|n[ºo]|#|piso|edif(?:icio)?)\.?\s*\S+/gi, '');
  s = s.replace(/\s{2,}/g, ' ').trim();
  return s.length > 0 ? s : undefined;
}

// Lowercase + strip accents so "Padrón" and "padron" compare equal.
const normalize = (s: string) =>
  s.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');

// Noise words that carry no locating value; excluded from the disambiguation tokens.
const STOPWORDS = new Set([
  'entre', 'apto', 'apartamento', 'apt', 'calle', 'avenida', 'ave', 'esquina',
  'esq', 'piso', 'edificio', 'edif', 'del', 'los', 'las', 'con', 'por', 'reparto',
  'numero', 'num', 'cuba',
]);

// Significant tokens from the *raw* address (street incl. "entre X y Y", city, state).
// These are the local reference points a human uses — the cross streets and
// neighborhood — and let us pick the right match among several same-named streets.
function addressTokens(address?: Address | null): string[] {
  const raw = [address?.street, address?.city, address?.state].filter(Boolean).join(' ');
  return normalize(raw)
    .split(/[^a-z0-9]+/)
    .filter((t) => t.length >= 3 && !STOPWORDS.has(t));
}

interface GeoParts { street?: string; city?: string; state?: string; country?: string }
interface GeoHit { lat: number; lng: number; display: string }

async function nominatimSearch(parts: GeoParts): Promise<GeoHit[]> {
  // Ask for several candidates; Havana has multiple streets sharing a name.
  const params = new URLSearchParams({ format: 'json', limit: '10', addressdetails: '1' });
  if (parts.street)  params.set('street', parts.street);
  if (parts.city)    params.set('city', parts.city);
  if (parts.state)   params.set('state', parts.state);
  if (parts.country) params.set('country', parts.country);
  const cc = countryCode(parts.country);
  if (cc) params.set('countrycodes', cc);

  const res = await fetch(
    `https://nominatim.openstreetmap.org/search?${params.toString()}`,
    {
      headers: {
        'Accept-Language': 'es',
        // User-Agent is required by Nominatim ToS; without it requests get blocked
        'User-Agent': 'BusinessSearcher/1.0',
      },
    },
  );
  const data = await res.json();
  if (!Array.isArray(data)) return [];
  return data
    // Drop matches that landed in a different country than requested.
    .filter((h) => !cc || !h.address?.country_code || h.address.country_code.toLowerCase() === cc)
    .map((h) => ({ lat: parseFloat(h.lat), lng: parseFloat(h.lon), display: h.display_name ?? '' }));
}

// Among candidates, prefer the one whose display name shares the most reference
// tokens with what the user typed (cross streets, neighborhood, municipality).
// Ties keep Nominatim's own ranking (results are already ordered by importance).
function pickBest(hits: GeoHit[], tokens: string[]): GeoHit {
  let best = hits[0];
  let bestScore = -1;
  for (const h of hits) {
    const dn = normalize(h.display);
    const score = tokens.reduce((n, t) => (dn.includes(t) ? n + 1 : n), 0);
    if (score > bestScore) { bestScore = score; best = h; }
  }
  return best;
}

export interface GeocodeResult { lat: number; lng: number; query: string }

export async function geocodeAddress(address?: Address | null): Promise<GeocodeResult | null> {
  const tokens  = addressTokens(address);
  const street  = cleanStreet(address?.street);
  const city    = address?.city?.trim()    || undefined;
  const state   = address?.state?.trim()   || undefined;
  const country = address?.country?.trim() || undefined;

  // Progressively drop the most granular (and least geocodable) parts.
  // Cuban informal street addresses rarely resolve, so falling back to the
  // municipality/city centroid gives a correct-enough pin instead of a wrong one.
  // The country field stays on every attempt so the countrycodes filter keeps applying.
  const candidates: GeoParts[] = [
    { street, city, state, country },
    { city, state, country },
    { city, country },
  ];

  for (const parts of candidates) {
    // Skip attempts with no usable locality fields
    if (!parts.street && !parts.city && !parts.state) continue;
    const label = [parts.street, parts.city, parts.state, parts.country].filter(Boolean).join(', ');
    try {
      const hits = await nominatimSearch(parts);
      if (hits.length > 0) {
        const best = pickBest(hits, tokens);
        return { lat: best.lat, lng: best.lng, query: label };
      }
    } catch { /* try next */ }
  }
  return null;
}
