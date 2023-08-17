import numpy as np

class PortfolioOptimization:
    def __init__(self, expected_returns:dict[str,float], covariance_matrix:np.ndarray[int,np.dtype[np.float64]], risk_free_rate:float=0.0):
        self.expected_returns = expected_returns
        self.covariance_matrix = covariance_matrix
        self.risk_free_rate = risk_free_rate
        self.__array_expected_returns = np.array(list(expected_returns.values()))
        self.__number_of_assets = len(expected_returns)
    
    def __calculate_portfolio_return(self, weights:np.ndarray[int,np.dtype[np.float64]]) -> float:
        return np.dot(weights, self.__array_expected_returns)
    
    def __calculate_portfolio_variance(self, weights:np.ndarray[int,np.dtype[np.float64]]) -> float:
        return np.dot(weights.T, np.dot(self.covariance_matrix, weights))
    
    def __calculate_portfolio_risk(self, weights:np.ndarray[int,np.dtype[np.float64]]) -> float:
        return np.sqrt(self.__calculate_portfolio_variance(weights))
    
    def __calculate_portfolio_sharpe_ratio(self, weights:np.ndarray[int,np.dtype[np.float64]]) -> float:
        return (self.__calculate_portfolio_return(weights) - self.risk_free_rate) / self.__calculate_portfolio_risk(weights)
    
    @staticmethod
    def __add_column(covariance_matrix:np.ndarray[int,np.dtype[np.float64]], value:float=1.0):
        column_index = covariance_matrix.shape[1]
        return np.insert(covariance_matrix, column_index, value, axis=1)
    
    @staticmethod
    def __add_column_values(covariance_matrix:np.ndarray[int,np.dtype[np.float64]], values:np.ndarray[int,np.dtype[np.float64]]):
        column_index = covariance_matrix.shape[1]
        return np.insert(covariance_matrix, column_index, values, axis=1)
    
    @staticmethod
    def __add_row(covariance_matrix:np.ndarray[int,np.dtype[np.float64]], value:float=1.0):
        row_index = covariance_matrix.shape[0]
        return np.insert(covariance_matrix, row_index, value, axis=0)
    
    @staticmethod
    def __add_row_values(covariance_matrix:np.ndarray[int,np.dtype[np.float64]], values:np.ndarray[int,np.dtype[np.float64]]):
        row_index = covariance_matrix.shape[0]
        return np.insert(covariance_matrix, row_index, values, axis=0)
    
    def target_return_portfolio(self, target_return:float, use_risk_free:bool = False):
        covariance = self.covariance_matrix
        n_values = self.__number_of_assets
        expected_returns = self.__array_expected_returns
        if use_risk_free:
            covariance = self.__add_column(covariance,0)
            covariance = self.__add_row(covariance,0)
            covariance[self.__number_of_assets,self.__number_of_assets] = np.finfo(np.float64).epsneg
            n_values = self.__number_of_assets + 1
            expected_returns = np.append(expected_returns, self.risk_free_rate)
        dmat = 2*covariance
        ones = np.ones(n_values)
        dmat = self.__add_row_values(dmat, expected_returns)
        dmat = self.__add_row_values(dmat, ones)
        rets = np.append(expected_returns, [0,0])
        dmat = self.__add_column_values(dmat, rets)
        ones = np.ones(n_values)
        ones = np.append(ones, [0, 0])
        dmat = self.__add_column_values(dmat, ones)
        bvec = np.zeros(n_values)
        bvec = np.append(bvec, [target_return, 1])
        sol = np.linalg.solve(dmat, bvec)
        return sol[0:n_values]

if __name__ == "__main__":
    expected_returns = {
        "AAPL": 0.2,
        "MSFT": 0.3,
        "GOOG": 0.25,
    }
    covariance_matrix = np.array([
        [0.2, 0.12, 0.1],
        [0.12, 0.3, 0.15],
        [0.1, 0.15, 0.3]
    ])
    portfolio_optimization = PortfolioOptimization(expected_returns, covariance_matrix, 0.05)
    print(portfolio_optimization.target_return_portfolio(0.25, use_risk_free=True))