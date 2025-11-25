const { getSqlConfig, validateSqlConfig, getSqlErrorMessage, sql } = require('./sql-config');
const { decodeUserFromAuthHeader } = require('./auth-utils');

function addCorsHeaders(res) {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
  res.setHeader('Access-Control-Max-Age', '86400');
}

module.exports = async (req, res) => {
  if (req.method === 'OPTIONS') {
    addCorsHeaders(res);
    return res.status(200).end();
  }

  addCorsHeaders(res);

  if (req.method !== 'POST') {
    return res.status(405).json({ error: 'Method not allowed' });
  }

  const user = decodeUserFromAuthHeader(req.headers.authorization);
  if (!user || user.role !== 'admin') {
    return res.status(403).json({ error: 'Only admins can toggle features' });
  }

  const body = req.body || {};
  const featureId = body.featureId;

  if (!Number.isInteger(featureId)) {
    return res.status(400).json({ error: 'featureId is required' });
  }

  let pool;
  try {
    const config = await getSqlConfig();
    const validation = validateSqlConfig(config);
    if (!validation.isValid) {
      return res.status(500).json(validation.error);
    }

    pool = await sql.connect(config);

    const featureResult = await pool.request()
      .input('FeatureId', sql.Int, featureId)
      .query(`
        SELECT Id, FeatureNumber, IsEnabled
        FROM Features
        WHERE Id = @FeatureId
      `);

    if (featureResult.recordset.length === 0) {
      return res.status(404).json({ error: `Feature with id ${featureId} not found` });
    }

    const currentFeature = featureResult.recordset[0];
    const newState = !currentFeature.IsEnabled;
    const enabledDate = newState ? new Date() : null;
    const enabledBy = newState ? user.username : null;

    await pool.request()
      .input('FeatureId', sql.Int, featureId)
      .input('IsEnabled', sql.Bit, newState)
      .input('EnabledDate', sql.DateTime2, enabledDate)
      .input('EnabledBy', sql.NVarChar(100), enabledBy)
      .query(`
        UPDATE Features
        SET IsEnabled = @IsEnabled,
            EnabledDate = @EnabledDate,
            EnabledBy = @EnabledBy
        WHERE Id = @FeatureId
      `);

    return res.status(200).json({
      success: true,
      isEnabled: newState,
      enabledDate: enabledDate ? enabledDate.toISOString() : null,
      enabledBy
    });
  } catch (error) {
    console.error('Error toggling feature:', error);
    const details = getSqlErrorMessage(error);
    return res.status(500).json({
      error: 'Failed to toggle feature',
      details
    });
  } finally {
    if (pool) {
      try {
        await pool.close();
      } catch (closeError) {
        console.warn('Failed to close SQL connection after ToggleFeature', closeError);
      }
    }
  }
};

