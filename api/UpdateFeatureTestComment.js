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

  const body = req.body || {};
  const featureId = body.featureId;
  const testComment = (body.testComment || '').trim();

  if (!Number.isInteger(featureId) || !testComment) {
    return res.status(400).json({ error: 'featureId and testComment are required' });
  }

  const user = decodeUserFromAuthHeader(req.headers.authorization);
  const commentedBy = user?.username || 'anonymous';
  const commentDate = new Date();

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
      .query(`SELECT Id FROM Features WHERE Id = @FeatureId`);

    if (featureResult.recordset.length === 0) {
      return res.status(404).json({ error: `Feature with id ${featureId} not found` });
    }

    await pool.request()
      .input('FeatureId', sql.Int, featureId)
      .input('TestComment', sql.NVarChar(5000), testComment)
      .input('TestCommentBy', sql.NVarChar(100), commentedBy)
      .input('TestCommentDate', sql.DateTime2, commentDate)
      .query(`
        UPDATE Features
        SET TestComment = @TestComment,
            TestCommentBy = @TestCommentBy,
            TestCommentDate = @TestCommentDate
        WHERE Id = @FeatureId
      `);

    return res.status(200).json({
      success: true,
      testComment,
      testCommentBy: commentedBy,
      testCommentDate: commentDate.toISOString()
    });
  } catch (error) {
    console.error('Error updating feature test comment:', error);
    const details = getSqlErrorMessage(error);
    return res.status(500).json({
      error: 'Failed to update test comment',
      details
    });
  } finally {
    if (pool) {
      try {
        await pool.close();
      } catch (closeError) {
        console.warn('Failed to close SQL connection after UpdateFeatureTestComment', closeError);
      }
    }
  }
};

