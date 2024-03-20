FindBestArchModel <- function(x, type_models = c(
                                     "eGARCH", "sGARCH",
                                     "csGARCH", "iGARCH",
                                     "gjrGARCH", "apARCH"
                                 ),
                                 dist_to_use = c(
                                     "std", "sstd", "jsu", "nig",
                                     "norm", "snorm", "ged", "sged"
                                 ),
                                 max_AR = 1, max_MA = 1, max_ARCH = 2, max_GARCH = 2,
                                 min_ARCH = 1, min_GARCH = 1, min_AR = 0, min_MA = 0,
                                 external.regressors = NULL, include.mean = c(T, F),
                                 arfima = F, simplified = T) {
    `%>%` <- magrittr::`%>%`
    if (simplified) {
        message("Distribution fitting")
        a <- lapply(dist_to_use, rugarch::fitdist, x = x)
        b <- sapply(a, \(x)min(x$values))
        dist_to_use <- dist_to_use[which.min(b)]
        expanded <- tidyr::expand_grid(include.mean,
            criterion = c("AIC", "BIC"),
            arfima
        )
        message("ARFIMA model")
        auto <- purrr::pmap(
            list(
                criterion = expanded$criterion,
                include.mean = expanded$include.mean,
                arfima = expanded$arfima
            ),
            rugarch::autoarfima,
            data = x, ar.max = max_AR, ma.max = max_MA,
            method = "partial", external.regressors = external.regressors,
            distribution.model = dist_to_use
        )
        auto_m <- lapply(auto, \(x)as.data.frame(x$rank.matrix)) %>%
            dplyr::bind_rows() %>%
            `row.names<-`(NULL) %>%
            dplyr::mutate(BIC = ifelse(is.na(BIC), 0, BIC), AIC = ifelse(is.na(AIC), 0, AIC))
        aic <- auto_m[which.min(auto_m$AIC), ]
        bic <- auto_m[which.min(auto_m$BIC), ]
        include.mean <- unique(c(aic$Mean, bic$Mean) == 1)
        max_AR <- max(aic$AR, bic$AR)
        max_MA <- max(aic$MA, bic$MA)
        min_AR <- min(aic$AR, bic$AR)
        min_MA <- min(aic$MA, bic$MA)
        arfima <- unique(c(aic$ARFIMA, bic$ARFIMA)) == 1
        type_mod <- tidyr::expand_grid(
            type_models = type_models,
            dist_to_use = dist_to_use,
            arma_lag = min_AR:max_AR, ma_lag = min_MA:max_MA,
            arch_lag = 1, garch_lag = 1,
            include.mean, arfima
        )
        message("Best GARCH model")
        type_mods <- purrr::pmap(
            list(
                type_model = type_mod$type_models,
                type_dist = type_mod$dist_to_use,
                lag_ar = type_mod$arma_lag,
                lag_ma = type_mod$ma_lag,
                lag_arch = type_mod$arch_lag,
                lag_garch = type_mod$garch_lag,
                include.mean = type_mod$include.mean,
                arfima = type_mod$arfima
            ),
            DoSingleGarch,
            x = x, external.regressors = external.regressors
        )
        type_mods <- lapply(type_mods, function(x) x$table) %>% dplyr::bind_rows()
        bic <- type_mods[which.min(type_mods$BIC), ]
        aic <- type_mods[which.min(type_mods$AIC), ]
        model_bic <- bic$type_model
        model_aic <- aic$type_model
        type_models <- unique(c(model_bic, model_aic))
        min_MA <- min(aic$lag_ma, bic$lag_ma)
        min_AR <- min(aic$lag_ar, bic$lag_ar)
        max_AR <- max(aic$lag_ar, bic$lag_ar)
        max_MA <- max(aic$lag_ma, bic$lag_ma)
        include.mean <- unique(c(aic$include.mean, bic$include.mean))
        arfima <- unique(c(aic$arfima, bic$arfima))
    }
    df_grid <- tidyr::expand_grid(
        type_models = type_models,
        dist_to_use = dist_to_use,
        arma_lag = min_AR:max_AR,
        ma_lag = min_MA:max_MA,
        arch_lag = min_ARCH:max_ARCH,
        garch_lag = min_GARCH:max_GARCH,
        include.mean, arfima
    )
    message("Best model")
    l_out <- purrr::pmap(
        .l = list(
            type_model = df_grid$type_models,
            type_dist = df_grid$dist_to_use,
            lag_ar = df_grid$arma_lag,
            lag_ma = df_grid$ma_lag,
            lag_arch = df_grid$arch_lag,
            lag_garch = df_grid$garch_lag,
            include.mean = df_grid$include.mean,
            arfima = df_grid$arfima
        ),
        DoSingleGarch, x = x, external.regressors = external.regressors
    )
    tab_out <- lapply(l_out, function(x) x$table) %>%
        dplyr::bind_rows()
    fits <- lapply(l_out, function(x) x$fit)
    # find by AIC
    idx <- which.min(tab_out$AIC)
    best_aic <- tab_out[idx, ]
    fit_aic <- fits[idx][[1]]
    # find by BIC
    idx <- which.min(tab_out$BIC)
    best_bic <- tab_out[idx, ]
    fit_bic <- fits[idx][[1]]
    l_out <- list(
        best_aic = best_aic,
        best_bic = best_bic,
        tab_out = tab_out,
        fit_bic = fit_bic,
        fit_aic = fit_aic,
        ugspec_b = rugarch::getspec(fit_bic),
        ugspec_a = rugarch::getspec(fit_aic)
    )
    return(l_out)
}
DoSingleGarch <- function(x, type_model, type_dist, lag_ar,
                            lag_ma, lag_arch, lag_garch, include.mean = T,
                            arfima = F, external.regressors = NULL) {
    spec <- NA
    message("Estimating ARMA(", lag_ar, ",", lag_ma, ")-",
        type_model, "(", lag_arch, ",", lag_garch, ")",
        " dist = ", type_dist,
        appendLF = FALSE
    )
    spec <- rugarch::ugarchspec(
        variance.model = list(
            model = type_model,
            garchOrder = c(lag_arch, lag_garch)
        ),
        mean.model = list(
            armaOrder = c(lag_ar, lag_ma),
            external.regressors = external.regressors,
            arfima = arfima,
            include.mean = include.mean
        ),
        distribution = type_dist
    )
    try(
        {
            my_rugarch <- list()
            my_rugarch <- rugarch::ugarchfit(spec = spec, data = x)
        },
        silent = T
    )
    if (!rugarch::convergence(my_rugarch)) {
        message("\tDone")
        criterion <- rugarch::infocriteria(my_rugarch)
        AIC <- criterion[1]
        BIC <- criterion[2]
    } else {
        message("\tEstimation failed..")
        AIC <- NA
        BIC <- NA
    }

    est_tab <- dplyr::tibble(lag_ar, lag_ma, lag_arch, lag_garch, include.mean, arfima,
        AIC = AIC, BIC = BIC, type_model = type_model,
        type_dist, model_name = paste0(
            "ARMA(", lag_ar, ",", lag_ma, ")+",
            type_model, "(", lag_arch, ",", lag_garch, ") ",
            type_dist
        )
    )
    lista <- list(table = est_tab, fit = my_rugarch)
    return(lista)
}
DoSingleDcc <- function(x, uspec,
                          type_model = "DCC",
                          type_dist = "mvnorm",
                          order1,
                          order2,
                          fit.control,
                          fit = NULL) {
    spec <- rmgarch::dccspec(uspec,
        dccOrder = c(order1, order2),
        model = type_model, distribution = type_dist
    )
    message("Estimating DCC(", order1, ",", order2, ")-",
        type_model, " dist = ", type_dist,
        appendLF = FALSE
    )
    try({
        my_rugarch <- list()
        my_rugarch <- rmgarch::dccfit(spec = spec, data = x, fit = fit, fit.control = fit.control)
    })
    if (!is.null(coef(my_rugarch))) {
        message("\tDone")
        AIC <- rugarch::infocriteria(my_rugarch)[1]
        BIC <- rugarch::infocriteria(my_rugarch)[2]
    } else {
        message("\tEstimation failed..")
        AIC <- NA
        BIC <- NA
    }
    est_tab <- dplyr::tibble(
        order1 = order1,
        order2 = order2,
        AIC = AIC,
        BIC = BIC,
        type_model = type_model,
        type_dist = type_dist
    )
    lista <- list(
        table = est_tab,
        fit = my_rugarch
    )
    return(lista)
}
FindBestDccModel <- function(x, uspec, type_models = "DCC",
                                dist_to_use = "mvnorm",
                                max_order1 = 2, max_order2 = 2,
                                min_order1 = 1, min_order2 = 1,
                                fit.control = list(
                                    eval.se = F,
                                    stationarity = T,
                                    scale = F
                                ),
                                fit = NULL) {
    `%>%` <- magrittr::`%>%`
    df_grid <- tidyr::expand_grid(
        type_models = type_models,
        dist_to_use = dist_to_use,
        order1 = min_order1:max_order1,
        order2 = min_order2:max_order2
    )
    l_out <- purrr::pmap(
        .l = list(
            uspec = rep(list(uspec), nrow(df_grid)),
            fit = rep(list(fit), nrow(df_grid)),
            type_model = df_grid$type_models,
            type_dist = df_grid$dist_to_use,
            order1 = df_grid$order1,
            order2 = df_grid$order2
        ),
        DoSingleDcc,
        fit.control = fit.control, x = x
    )
    tab_out <- lapply(l_out, function(x) x$table) %>%
        dplyr::bind_rows()
    fits <- sapply(l_out, function(x) x$fit)
    # find by AIC
    idx <- which.min(tab_out$AIC)
    best_aic <- tab_out[idx, ]
    fit_aic <- fits[idx][[1]]
    # find by BIC
    idx <- which.min(tab_out$BIC)
    best_bic <- tab_out[idx, ]
    fit_bic <- fits[idx][[1]]
    l_out <- list(
        best_aic = best_aic,
        best_bic = best_bic,
        tab_out = tab_out,
        fit_bic = fit_bic,
        fit_aic = fit_aic,
        dccspec_b = rmgarch::dccspec(uspec,
            dccOrder = c(best_bic$order1, best_bic$order2),
            distribution = best_bic$type_dist,
            model = best_bic$type_model
        ),
        dccspec_a = rmgarch::dccspec(uspec,
            dccOrder = c(best_aic$order1, best_aic$order2),
            distribution = best_aic$type_dist,
            model = best_aic$type_model
        )
    )

    return(l_out)
}
