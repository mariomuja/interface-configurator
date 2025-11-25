const { getSqlConfig, validateSqlConfig, getSqlErrorMessage, sql } = require('./sql-config');
const { decodeUserFromAuthHeader } = require('./auth-utils');

function addCorsHeaders(res) {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
  res.setHeader('Access-Control-Max-Age', '86400');
}

function mapFeature(row, userRole) {
  const toIso = (value) => (value ? new Date(value).toISOString() : undefined);

  return {
    id: row.Id,
    featureNumber: row.FeatureNumber,
    title: row.Title,
    description: row.Description,
    detailedDescription: row.DetailedDescription,
    technicalDetails: row.TechnicalDetails,
    testInstructions: row.TestInstructions,
    knownIssues: row.KnownIssues,
    dependencies: row.Dependencies,
    breakingChanges: row.BreakingChanges,
    screenshots: row.Screenshots,
    category: row.Category,
    priority: row.Priority,
    isEnabled: !!row.IsEnabled,
    implementedDate: toIso(row.ImplementedDate),
    enabledDate: toIso(row.EnabledDate),
    enabledBy: row.EnabledBy || undefined,
    testComment: row.TestComment || undefined,
    testCommentBy: row.TestCommentBy || undefined,
    testCommentDate: toIso(row.TestCommentDate),
    canToggle: userRole === 'admin'
  };
}

module.exports = async (req, res) => {
  if (req.method === 'OPTIONS') {
    addCorsHeaders(res);
    return res.status(200).end();
  }

  addCorsHeaders(res);

  if (req.method !== 'GET') {
    return res.status(405).json({ error: 'Method not allowed' });
  }

  let pool;
  try {
    const config = await getSqlConfig();
    const validation = validateSqlConfig(config);
    if (!validation.isValid) {
      return res.status(500).json(validation.error);
    }

    pool = await sql.connect(config);
    const result = await pool.request().query(`
      SELECT Id, FeatureNumber, Title, Description, DetailedDescription, TechnicalDetails,
             TestInstructions, KnownIssues, Dependencies, BreakingChanges, Screenshots,
             Category, Priority, IsEnabled, ImplementedDate, EnabledDate, EnabledBy,
             TestComment, TestCommentBy, TestCommentDate
      FROM Features
      ORDER BY ImplementedDate DESC
    `);

    const user = decodeUserFromAuthHeader(req.headers.authorization);
    const features = result.recordset.map(row => mapFeature(row, user?.role));
    return res.status(200).json(features);
  } catch (error) {
    console.error('Error fetching features:', error);
    const details = getSqlErrorMessage(error);
    return res.status(500).json({
      error: 'Failed to load features',
      details
    });
  } finally {
    if (pool) {
      try {
        await pool.close();
      } catch (closeError) {
        console.warn('Failed to close SQL connection after GetFeatures', closeError);
      }
    }
  }
};

