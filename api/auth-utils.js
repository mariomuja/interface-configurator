function decodeUserFromAuthHeader(authHeader) {
  if (!authHeader) {
    return null;
  }

  const headerValue = Array.isArray(authHeader) ? authHeader[0] : authHeader;
  if (!headerValue) {
    return null;
  }

  const token = headerValue.replace(/^Bearer\s+/i, '').trim();
  if (!token) {
    return null;
  }

  try {
    const decoded = Buffer.from(token, 'base64').toString('utf8');
    const [username, role] = decoded.split(':');
    if (!username || !role) {
      return null;
    }
    return {
      username,
      role
    };
  } catch (error) {
    console.warn('Failed to decode auth header token', error);
    return null;
  }
}

module.exports = {
  decodeUserFromAuthHeader
};

