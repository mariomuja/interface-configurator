module.exports = async function (context, req) {
    context.log('SimpleTestFunction executed successfully!');
    
    const responseMessage = 'SimpleTestFunction is working! ✅';
    
    context.res = {
        status: 200,
        body: responseMessage,
        headers: {
            'Content-Type': 'text/plain; charset=utf-8'
        }
    };
};
