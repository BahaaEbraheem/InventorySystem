using InventorySystem.Application.DTOs.Purchase;
using InventorySystem.Application.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Domain.Exceptions;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchasesController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;
    private readonly ILogger<PurchasesController> _logger;

    public PurchasesController(
        IPurchaseService purchaseService,
        ILogger<PurchasesController> logger)
    {
        _purchaseService = purchaseService;
        _logger = logger;
    }

    [HttpPost("{id}/submit")]
    [ProducesResponseType(typeof(BaseResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<PurchaseOrderDto>>> SubmitOrder(
    Guid id,
    CancellationToken cancellationToken)
    {
        var response = await _purchaseService.SubmitPurchaseOrderAsync(id, cancellationToken);

        if (!response.Success)
        {
            return response.Code switch
            {
                ResponseCodes.NotFound => NotFound(response),
                ResponseCodes.BusinessRuleViolation or ResponseCodes.ValidationError => BadRequest(response),
                ResponseCodes.ConcurrencyError => Conflict(response),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response)
            };
        }

        return Ok(response);
    }



    [HttpPost]
    [ProducesResponseType(typeof(BaseResponse<CreatePurchaseOrderResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<CreatePurchaseOrderResponse>>> Create(
        [FromBody] CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        // لا حاجة لـ try-catch! الـ Middleware سيتعامل مع كل شيء
        var response = await _purchaseService.CreatePurchaseOrderAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.PurchaseOrderId },
            BaseResponse<CreatePurchaseOrderResponse>.SuccessResponse(response,"Purchase order created successfully"));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(BaseResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse<PurchaseOrderDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _purchaseService.GetPurchaseOrderByIdAsync(id, cancellationToken);

        if (order == null)
        {
            // رمي استثناء بدلاً من إرجاع NotFound يدوياً - الـ Middleware سيتعامل معه
            throw new NotFoundException(nameof(PurchaseOrder), id);
        }

        return BaseResponse<PurchaseOrderDto>.SuccessResponse(
            order,
            "Purchase order retrieved successfully");
    }


    [HttpPost("{id}/receive")]
    [ProducesResponseType(typeof(BaseResponse<ReceivePurchaseOrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BaseResponse<ReceivePurchaseOrderResponse>>> ReceiveOrder(
    Guid id,
    [FromBody] List<ReceiveOrderItemRequest> receivedItems,
    CancellationToken cancellationToken)
    {
        if (receivedItems == null || receivedItems.Count == 0)
            return BadRequest(BaseResponse<ReceivePurchaseOrderResponse>.ErrorResponse(
                ResponseCodes.ValidationError, "Received items list cannot be empty"));

        var response = await _purchaseService.ReceivePurchaseOrderAsync(id, receivedItems, cancellationToken);

        if (!response.Success)
        {
            return response.Code switch
            {
                ResponseCodes.NotFound => NotFound(response),
                ResponseCodes.BusinessRuleViolation or ResponseCodes.ValidationError => BadRequest(response),
                ResponseCodes.ConcurrencyError => Conflict(response),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response)
            };
        }

        return Ok(response);
    }


    /// إلغاء طلب شراء (مسموح فقط من حالة Draft أو Submitted)
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(typeof(BaseResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BaseResponse<PurchaseOrderDto>>> CancelOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _purchaseService.CancelPurchaseOrderAsync(id, cancellationToken);

        if (!response.Success)
        {
            return response.Code switch
            {
                ResponseCodes.NotFound => NotFound(response),
                ResponseCodes.BusinessRuleViolation => BadRequest(response),
                _ => StatusCode(StatusCodes.Status500InternalServerError, response)
            };
        }

        return Ok(response);
    }
}
