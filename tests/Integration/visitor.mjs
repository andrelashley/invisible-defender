import WebSocket from '../../src/web/node_modules/ws/index.js';
export function visitor(base, transport = globalThis.fetch) {
  let cookie = '';
  return {
    async fetch(path, options = {}) {
      const response = await transport(`${base}${path}`, { ...options, headers: { ...options.headers, ...(cookie ? { Cookie: cookie } : {}) } });
      const set = response.headers.get('set-cookie');
      if (set) cookie = set.split(';')[0];
      return response;
    },
    socket(path) { return new WebSocket(`${base.replace(/^http/, 'ws')}${path}`, { headers: { Cookie: cookie } }); }
  };
}

